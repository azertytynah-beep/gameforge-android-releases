using System;
using System.IO;
using System.Linq;
using GameForge.Editor;
using GameForge.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace GameForge.Tests.EditMode.Android
{
    public sealed class AndroidApkExportTests
    {
        private string temporary;
        private string variantPath;

        [SetUp]
        public void SetUp()
        {
            temporary = Path.Combine(Path.GetTempPath(), "GameForge-Export-Test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temporary);
        }

        [TearDown]
        public void TearDown()
        {
            if (variantPath != null && AssetDatabase.IsValidFolder(variantPath))
                AssetDatabase.DeleteAsset(variantPath);
            variantPath = null;

            File.Delete(Path.Combine(temporary, "settings.json"));
            Directory.Delete(temporary);
        }

        [Test]
        public void MenuAndBatchResolveTheSameVariantAndRenameKeepsItsApplicationId()
        {
            var id = Guid.NewGuid();
            variantPath = GenerationStore.GeneratedRoot + "/" + id.ToString("N");
            var store = new GenerationStore(() => id);
            var generator = new RunnerGenerator(store,
                AssetDatabase.LoadAssetAtPath<RunnerCatalog>(RunnerGenerator.CatalogPath));
            var result = generator.Generate(RunnerConfigV1.CreateDefault());
            Assert.That(result.Succeeded, Is.True, string.Join("\n", result.Diagnostics.Select(item => item.Message)));
            var profile = AssetDatabase.LoadAssetAtPath<AndroidTestProfile>(AndroidApkExport.ProfilePath);
            Assert.That(profile, Is.Not.Null);

            var viaMenu = AndroidApkExport.VariantIdFromPath(result.ScenePath);
            var viaBatch = AndroidApkExport.ParseBatchId(new[] { "-buildTarget", "Android", "-gameForgeVariant", result.VariantId });
            var first = AndroidApkExport.Resolve(store.ReadVariant(viaMenu), profile, out _);
            var second = AndroidApkExport.Resolve(store.ReadVariant(viaBatch), profile, out _);
            Assert.That(JsonUtility.ToJson(second), Is.EqualTo(JsonUtility.ToJson(first)));
            Assert.That(first.applicationId, Is.EqualTo("com.gameforge.test.v" + result.VariantId));
            Assert.That(first.orientation, Is.EqualTo("Portrait"));
            Assert.That(first.architecture, Is.EqualTo("ARM64"));
            Assert.That(first.graphicsApi, Is.EqualTo("OpenGLES3"));

            var renamed = AndroidApkExport.Resolve(store.Rename(result.VariantId, "Course renommée"), profile, out _);
            Assert.That(renamed.applicationId, Is.EqualTo(first.applicationId));
            Assert.That(renamed.scene, Is.EqualTo(first.scene));
            Assert.That(renamed.title, Is.EqualTo("Course renommée"));
            Assert.That(AndroidApkExport.ApplicationId(Guid.NewGuid().ToString("N")), Is.Not.EqualTo(first.applicationId));
        }

        [Test]
        public void MissingVariantSceneAndAmbiguousBatchArgumentsAreRejected()
        {
            var id = Guid.NewGuid().ToString("N");
            var manifest = new VariantManifest
            {
                variantId = id, scene = "Runner.unity", settings = "RunnerSettings.asset", title = "Test"
            };
            var missing = new StoredVariant(GenerationStore.GeneratedRoot + "/" + id, manifest);
            var profile = AssetDatabase.LoadAssetAtPath<AndroidTestProfile>(AndroidApkExport.ProfilePath);
            Assert.Throws<InvalidDataException>(() => AndroidApkExport.Resolve(missing, profile, out _));
            Assert.Throws<ArgumentException>(() => AndroidApkExport.ParseBatchId(new[] { "-gameForgeVariant", "../" }));
            Assert.Throws<ArgumentException>(() => AndroidApkExport.ParseBatchId(
                new[] { "-gameForgeVariant", id, "-gameForgeVariant", id }));
            Assert.That(AndroidApkExport.VariantIdFromPath("Assets/GameForgeGenerated/_staging/" + id), Is.Null);
        }

        [Test]
        public void NewBuildDirectoryPreservesExistingArtifacts()
        {
            var id = Guid.NewGuid().ToString("N");
            var first = AndroidApkExport.NewBuildDirectory(temporary, id);
            var second = AndroidApkExport.NewBuildDirectory(temporary, id);
            var marker = Path.Combine(first, "gameforge-android-test.apk");
            try
            {
                File.WriteAllText(marker, "premier export");
                Assert.That(first, Is.Not.EqualTo(second));
                Assert.That(File.ReadAllText(marker), Is.EqualTo("premier export"));
                Assert.That(Directory.GetFileSystemEntries(second), Is.Empty);
            }
            finally
            {
                File.Delete(marker);
                Directory.Delete(first);
                Directory.Delete(second);
                Directory.Delete(Path.Combine(temporary, id));
            }
        }

        [TestCase("success")]
        [TestCase("failure")]
        [TestCase("cancel")]
        public void JournalRestoresStateForEveryBuildOutcome(string outcome)
        {
            var path = Path.Combine(temporary, "settings.json");
            var state = new AndroidBuildSettings
            {
                applicationId = "com.gameforge.original", title = "Original", versionCode = 1
            };
            var planned = new AndroidBuildSettings
            {
                applicationId = "com.gameforge.test", title = "Test", versionCode = 2,
                overrideDefaultApplicationIdentifier = true
            };
            void Run() => AndroidBuildJournal.Run(path, planned, () => state, replacement => state = replacement, () =>
            {
                Assert.That(File.Exists(path), Is.True);
                Assert.That(state, Is.SameAs(planned));
                if (outcome == "cancel")
                    throw new OperationCanceledException();
                if (outcome == "failure")
                    throw new IOException("Échec simulé.");
            });

            if (outcome == "cancel")
                Assert.Throws<OperationCanceledException>(() => Run());
            else if (outcome == "failure")
                Assert.Throws<IOException>(() => Run());
            else
                Run();
            Assert.That(state.applicationId, Is.EqualTo("com.gameforge.original"));
            Assert.That(state.title, Is.EqualTo("Original"));
            Assert.That(state.versionCode, Is.EqualTo(1));
            Assert.That(state.overrideDefaultApplicationIdentifier, Is.False);
            Assert.That(File.Exists(path), Is.False);
        }

        [Test]
        public void RecoveryAcceptsASequenceInterruptedBetweenFieldsAndRetainsForeignChanges()
        {
            var path = Path.Combine(temporary, "settings.json");
            var previous = new AndroidBuildSettings { title = "Original", versionCode = 1 };
            var planned = new AndroidBuildSettings { title = "Test", versionCode = 2 };
            AndroidBuildJournal.Begin(path, previous, planned);
            var state = new AndroidBuildSettings { title = "Test", versionCode = 1 };
            AndroidBuildJournal.Recover(path, () => state, replacement => state = replacement);
            Assert.That(state.title, Is.EqualTo(previous.title));
            Assert.That(state.versionCode, Is.EqualTo(previous.versionCode));

            AndroidBuildJournal.Begin(path, previous, planned);
            state = new AndroidBuildSettings { title = "Modification utilisateur", versionCode = 1 };
            Assert.Throws<InvalidOperationException>(() => AndroidBuildJournal.Recover(
                path, () => state, _ => Assert.Fail("Un réglage utilisateur ne doit pas être modifié.")));
            Assert.That(File.Exists(path), Is.True);
        }
    }
}
