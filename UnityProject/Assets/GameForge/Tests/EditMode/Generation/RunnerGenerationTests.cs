using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using GameForge.Editor;
using GameForge.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace GameForge.Tests.EditMode.Generation
{
    public sealed class RunnerGenerationTests
    {
        private readonly List<Guid> fixtureIds = new List<Guid>();
        private string fixtureScene;

        [TearDown]
        public void Cleanup()
        {
            if (fixtureScene != null)
                AssetDatabase.DeleteAsset(fixtureScene);
            fixtureScene = null;

            foreach (var id in fixtureIds)
                DeleteFixture(id);
            fixtureIds.Clear();
        }

        [Test]
        public void CopiesOneConfigurationIntoTwoIndependentVariants()
        {
            var store = CreateStore();
            var generator = new RunnerGenerator(store, LoadCatalog());
            var config = RunnerConfigV1.CreateDefault("theme-night", "obstacle-barrier");
            config.title = "Nocturne";
            config.seed = 741;
            var sourcePath = Path.Combine(Application.dataPath, "..", RunnerGenerator.SourceScenePath);
            var source = File.ReadAllBytes(sourcePath);
            var active = SceneManager.GetActiveScene();

            var first = ReadGenerated(store, generator.Generate(config));
            var second = ReadGenerated(store, generator.Generate(config));
            Assert.That(second.Id, Is.Not.EqualTo(first.Id));
            Assert.That(second.ScenePath, Is.Not.EqualTo(first.ScenePath));
            Assert.That(second.Manifest.seed, Is.EqualTo(first.Manifest.seed));
            Assert.That(second.Manifest.themeId, Is.EqualTo(first.Manifest.themeId));
            Assert.That(second.Manifest.obstacleIds, Is.EqualTo(first.Manifest.obstacleIds));
            Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(active));
            Assert.That(File.ReadAllBytes(sourcePath), Is.EqualTo(source));

            var firstSettings = AssetDatabase.LoadAssetAtPath<RunnerSettings>(first.SettingsPath);
            var secondSettings = AssetDatabase.LoadAssetAtPath<RunnerSettings>(second.SettingsPath);
            Assert.That(secondSettings, Is.Not.SameAs(firstSettings));
            Assert.That(JsonUtility.ToJson(secondSettings.ToConfig()), Is.EqualTo(JsonUtility.ToJson(config)));
            InspectScene(first.ScenePath, scene =>
            {
                var bootstrap = scene.GetRootGameObjects().Single().GetComponent<RunnerBootstrap>();
                Assert.That(new SerializedObject(bootstrap).FindProperty("settings").objectReferenceValue,
                    Is.EqualTo(firstSettings));
            });
        }

        [Test]
        public void RenamingKeepsIdentityAndDuplicationPreservesSavedSceneEdits()
        {
            var store = CreateStore();
            var generator = new RunnerGenerator(store, LoadCatalog());
            var first = ReadGenerated(store, generator.Generate(RunnerConfigV1.CreateDefault()));
            InspectScene(first.ScenePath, scene =>
            {
                scene.GetRootGameObjects().Single().name = "Variante modifiée";
                EditorSceneManager.MarkSceneDirty(scene);
                Assert.That(EditorSceneManager.SaveScene(scene), Is.True);
            });

            var renamed = store.Rename(first.Id, "Nouveau titre");
            Assert.That(renamed.Id, Is.EqualTo(first.Id));
            Assert.That(renamed.ScenePath, Is.EqualTo(first.ScenePath));
            var reopened = new GenerationStore().ReadVariant(first.Id);
            Assert.That(reopened.Manifest.title, Is.EqualTo("Nouveau titre"));

            var duplicate = ReadGenerated(store, generator.Duplicate(reopened));
            Assert.That(duplicate.Id, Is.Not.EqualTo(first.Id));
            Assert.That(duplicate.Manifest.parentVariantId, Is.EqualTo(first.Id));
            Assert.That(duplicate.Manifest.title, Is.EqualTo(reopened.Manifest.title));
            InspectScene(duplicate.ScenePath, scene =>
            {
                var root = scene.GetRootGameObjects().Single();
                Assert.That(root.name, Is.EqualTo("Variante modifiée"));
                var settings = new SerializedObject(root.GetComponent<RunnerBootstrap>()).FindProperty("settings");
                Assert.That(AssetDatabase.GetAssetPath(settings.objectReferenceValue), Is.EqualTo(duplicate.SettingsPath));
            });
        }

        [Test]
        public void InvalidAndCancelledConfigurationsNeverBeginAWrite()
        {
            var writeAttempts = 0;
            var store = new GenerationStore(() =>
            {
                writeAttempts++;
                throw new AssertionException("L’écriture ne doit pas commencer.");
            });
            var generator = new RunnerGenerator(store, LoadCatalog());
            var config = RunnerConfigV1.CreateDefault();
            config.initialSpeed = -1f;

            var invalid = generator.Generate(config);
            Assert.That(invalid.Succeeded, Is.False);
            Assert.That(invalid.Diagnostics.Select(item => item.Field), Does.Contain("initialSpeed"));

            var cancelled = generator.Generate(RunnerConfigV1.CreateDefault(), new CancellationToken(true));
            Assert.That(cancelled.Succeeded, Is.False);
            Assert.That(cancelled.Diagnostics, Is.Not.Empty);
            Assert.That(writeAttempts, Is.Zero);
        }

        [Test]
        public void UsesTheSavedSourceAndKeepsTheOpenSceneDirty()
        {
            var template = CopySourceFixture();
            var store = CreateStore();
            InspectScene(template, scene =>
            {
                var root = scene.GetRootGameObjects().Single();
                var savedName = root.name;
                root.name = "Brouillon non enregistré";
                EditorSceneManager.MarkSceneDirty(scene);

                var generator = new RunnerGenerator(store, LoadCatalog(), template);
                var result = ReadGenerated(store, generator.Generate(RunnerConfigV1.CreateDefault()));
                Assert.That(scene.isDirty, Is.True);
                Assert.That(root.name, Is.EqualTo("Brouillon non enregistré"));
                InspectScene(result.ScenePath, copy => Assert.That(copy.GetRootGameObjects().Single().name,
                    Is.EqualTo(savedName)));
            });
        }

        [Test]
        public void MissingMotorReferencePreventsPublication()
        {
            var template = CopySourceFixture();
            InspectScene(template, scene =>
            {
                var bootstrap = new SerializedObject(scene.GetRootGameObjects().Single().GetComponent<RunnerBootstrap>());
                bootstrap.FindProperty("motor").objectReferenceValue = null;
                bootstrap.ApplyModifiedPropertiesWithoutUndo();
                Assert.That(EditorSceneManager.SaveScene(scene), Is.True);
            });

            var id = Guid.NewGuid();
            fixtureIds.Add(id);
            var store = new GenerationStore(() => id);
            var generator = new RunnerGenerator(store, LoadCatalog(), template);
            var result = generator.Generate(RunnerConfigV1.CreateDefault());
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Diagnostics, Is.Not.Empty);
            Assert.That(AssetDatabase.IsValidFolder(GenerationStore.GeneratedRoot + "/" + id.ToString("N")),
                Is.False);
        }

        private string CopySourceFixture()
        {
            fixtureScene = "Assets/GameForge/Tests/EditMode/Generation/Template-" + Guid.NewGuid().ToString("N") + ".unity";
            Assert.That(AssetDatabase.CopyAsset(RunnerGenerator.SourceScenePath, fixtureScene), Is.True);
            return fixtureScene;
        }

        private GenerationStore CreateStore() => new GenerationStore(() =>
        {
            var id = Guid.NewGuid();
            fixtureIds.Add(id);
            return id;
        });

        internal static RunnerCatalog LoadCatalog() => AssetDatabase.LoadAssetAtPath<RunnerCatalog>(RunnerGenerator.CatalogPath);

        internal static StoredVariant ReadGenerated(GenerationStore store, GenerationResult result)
        {
            var diagnostics = result.Diagnostics ?? Array.Empty<RunnerDiagnostic>();
            Assert.That(result.Succeeded, Is.True, string.Join("\n", diagnostics.Select(item => item.Message)));
            var variant = store.ReadVariant(result.VariantId);
            Assert.That(variant.ScenePath, Is.EqualTo(result.ScenePath));
            return variant;
        }

        internal static void DeleteFixture(Guid id)
        {
            foreach (var path in new[] { GenerationStore.GeneratedRoot + "/" + id.ToString("N"),
                GenerationStore.GeneratedRoot + "/_staging/" + id.ToString("N") })
            {
                var absolute = Path.GetFullPath(Path.Combine(Application.dataPath, "..", path));
                if (AssetDatabase.IsValidFolder(path) && OwnsFixture(absolute, id) &&
                    !Directory.EnumerateDirectories(absolute).Any())
                    AssetDatabase.DeleteAsset(path);
            }
        }

        internal static bool OwnsFixture(string path, Guid id)
        {
            var owner = Path.Combine(path, ".gameforge-owner");
            return Directory.Exists(path) && !File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint) &&
                File.Exists(owner) && !File.GetAttributes(owner).HasFlag(FileAttributes.ReparsePoint) &&
                File.ReadAllText(owner) == id.ToString("N");
        }

        private static void InspectScene(string path, Action<Scene> inspect)
        {
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                inspect(scene);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }

    public sealed class GeneratedRunnerFixture : IPrebuildSetup, IPostBuildCleanup
    {
        private const string Key = "GameForge.Tests.GeneratedRunnerFixture";
        private const string RestoredKey = "GameForge.Tests.RestoredRunnerFixture";
        private const string BackupKey = "GameForge.Tests.RunnerFixtureBackup";

        public void Setup()
        {
            var id = Guid.NewGuid();
            var restoredId = Guid.NewGuid();
            SessionState.SetString(Key, id.ToString("N"));
            SessionState.SetString(RestoredKey, restoredId.ToString("N"));
            try
            {
                var config = RunnerConfigV1.CreateDefault("theme-night", "obstacle-barrier");
                config.initialSpeed = 7f;
                var store = new GenerationStore(() => id);
                var generator = new RunnerGenerator(store, RunnerGenerationTests.LoadCatalog());
                var generated = RunnerGenerationTests.ReadGenerated(store, generator.Generate(config));
                var backupId = store.Backup(generated.Id);
                SessionState.SetString(BackupKey, backupId);
                var restored = new GenerationStore(() => restoredId).Restore(backupId);
                Assert.That(restored.Manifest.parentVariantId, Is.EqualTo(generated.Id));
            }
            catch
            {
                Cleanup();
                throw;
            }
        }

        public void Cleanup()
        {
            foreach (var key in new[] { Key, RestoredKey })
            {
                if (Guid.TryParseExact(SessionState.GetString(key, ""), "N", out var id))
                    RunnerGenerationTests.DeleteFixture(id);
                SessionState.EraseString(key);
            }

            if (Guid.TryParseExact(SessionState.GetString(BackupKey, ""), "N", out var backup))
                DeleteFixtureBackup(backup);
            SessionState.EraseString(BackupKey);
        }

        private static void DeleteFixtureBackup(Guid id)
        {
            var path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Backups", "GameForge", id.ToString("N")));
            if (!RunnerGenerationTests.OwnsFixture(path, id))
                return;
            var allowed = new[] { ".gameforge-owner", "Runner.unity", "Runner.unity.meta", "RunnerSettings.asset",
                "RunnerSettings.asset.meta", "GameForgeBackup.json" };
            var files = Directory.GetFileSystemEntries(path);
            if (files.Any(file => !allowed.Contains(Path.GetFileName(file)) ||
                (File.GetAttributes(file) & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0))
                throw new IOException("Fichier inattendu dans la sauvegarde de test.");

            foreach (var file in files)
                File.Delete(file);
            Directory.Delete(path);
        }
    }
}
