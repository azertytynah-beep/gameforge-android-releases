using System;
using System.Collections.Generic;
using System.IO;
using GameForge.Editor;
using GameForge.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameForge.Tests.EditMode.Storage
{
    public sealed class GenerationStoreTests
    {
        private readonly List<string> generated = new List<string>();
        private readonly List<string> backups = new List<string>();
        private readonly List<string> sharedAssets = new List<string>();

        [TearDown]
        public void Cleanup()
        {
            foreach (var path in generated)
            {
                if (AssetDatabase.IsValidFolder(path))
                    AssetDatabase.DeleteAsset(path);
            }

            foreach (var asset in sharedAssets)
                AssetDatabase.DeleteAsset(asset);

            foreach (var id in backups)
            {
                var directory = BackupPath(id);
                foreach (var file in Directory.EnumerateFiles(directory))
                    File.Delete(file);
                Directory.Delete(directory);
            }

            generated.Clear();
            sharedAssets.Clear();
            backups.Clear();
        }

        [Test]
        public void PublishesBothAssetsWithTheirGuidsAndFreezesIdentity()
        {
            var id = Guid.NewGuid();
            var store = new GenerationStore(() => id);
            using var transaction = Begin(store);
            WriteVariant(transaction);
            var sceneGuid = AssetDatabase.AssetPathToGUID(transaction.ScenePath);
            var settingsGuid = AssetDatabase.AssetPathToGUID(transaction.SettingsPath);
            var published = transaction.Commit("Marais");

            Assert.That(published.Id, Is.EqualTo(id.ToString("N")));
            Assert.That(AssetDatabase.AssetPathToGUID(published.ScenePath), Is.EqualTo(sceneGuid));
            Assert.That(AssetDatabase.AssetPathToGUID(published.SettingsPath), Is.EqualTo(settingsGuid));
            Assert.That(AssetDatabase.IsValidFolder(transaction.StagingPath), Is.False);
            var manifest = store.ReadVariant(published.Id).Manifest;
            Assert.That(manifest.title, Is.EqualTo("Marais"));
            Assert.That(manifest.seed, Is.EqualTo(42));
            published.Manifest.variantId = "mutated";
            Assert.That(published.Id, Is.EqualTo(id.ToString("N")));
        }

        [Test]
        public void ExistingVariantIsPreservedWhenAnIdCollides()
        {
            var id = Guid.NewGuid();
            var store = new GenerationStore(() => id);
            var published = Publish(store);
            var scene = File.ReadAllText(FullPath(published.ScenePath));

            Assert.Throws<IOException>(() => store.Begin());
            Assert.That(File.ReadAllText(FullPath(published.ScenePath)), Is.EqualTo(scene));
            Assert.That(store.ReadVariant(published.Id).Manifest.title, Is.EqualTo("Original"));
        }

        [Test]
        public void DestinationCreatedDuringStagingIsNeverOverwritten()
        {
            var store = new GenerationStore();
            using var transaction = Begin(store);
            WriteVariant(transaction);
            var path = GenerationStore.GeneratedRoot + "/" + transaction.Id;
            Assert.That(AssetDatabase.CreateFolder(GenerationStore.GeneratedRoot, transaction.Id), Is.Not.Empty);
            var marker = path + "/Keep.txt";
            File.WriteAllText(FullPath(marker), "keep me");
            AssetDatabase.ImportAsset(marker);

            Assert.Throws<IOException>(() => transaction.Commit("Collision"));
            Assert.That(AssetDatabase.IsValidFolder(transaction.StagingPath), Is.True);
            Assert.That(File.ReadAllText(FullPath(marker)), Is.EqualTo("keep me"));
            Assert.That(File.Exists(FullPath(path + "/Runner.unity")), Is.False);
        }

        [Test]
        public void InterruptedOperationRemainsDiagnosableAndReleasesTheLock()
        {
            var store = new GenerationStore();
            using var abandoned = Begin(store);
            Assert.Throws<IOException>(() => new GenerationStore().Begin());

            WriteScene(abandoned.ScenePath);
            Assert.Throws<InvalidDataException>(() => abandoned.Commit("Incomplete"));
            abandoned.Abort();

            Assert.That(store.ListInterrupted(), Does.Contain(abandoned.Id));
            Assert.That(AssetDatabase.IsValidFolder(abandoned.StagingPath), Is.True);
            Assert.That(AssetDatabase.IsValidFolder(GenerationStore.GeneratedRoot + "/" + abandoned.Id), Is.False);
            using var next = Begin(store);
            Assert.That(next.Id, Is.Not.EqualTo(abandoned.Id));
        }

        [TestCase("../outside")]
        [TestCase("C:/outside")]
        [TestCase("F0C40D3FE82340D883B2841D2A6F6A94")]
        public void RejectsUntrustedStorageIds(string path)
        {
            var store = new GenerationStore();
            Assert.Throws<ArgumentException>(() => store.ReadVariant(path));
            Assert.Throws<ArgumentException>(() => store.Restore(path));
        }

        [Test]
        public void RejectsMissingSerializedReferenceBeforePublication()
        {
            using var transaction = Begin(new GenerationStore());
            WriteVariant(transaction);
            var path = FullPath(transaction.ScenePath);
            var scene = File.ReadAllText(path);
            const string previous = "m_TargetTexture: {fileID: 0}";
            Assert.That(scene, Does.Contain(previous));
            var missingGuid = Guid.NewGuid().ToString("N");
            File.WriteAllText(path, scene.Replace(previous,
                $"m_TargetTexture: {{fileID: 8400000, guid: {missingGuid}, type: 2}}"));
            AssetDatabase.ImportAsset(transaction.ScenePath, ImportAssetOptions.ForceSynchronousImport);

            var error = Assert.Throws<InvalidDataException>(() => transaction.Commit("Missing"));
            Assert.That(error.Message, Does.Contain(missingGuid));
            Assert.That(AssetDatabase.IsValidFolder(GenerationStore.GeneratedRoot + "/" + transaction.Id), Is.False);
        }

        [Test]
        public void RestoresEditedSceneAndSettingsUnderNewIds()
        {
            var store = new GenerationStore();
            var original = Publish(store);
            WithScene(original.ScenePath, scene =>
            {
                scene.GetRootGameObjects()[0].name = "Caméra modifiée";
                EditorSceneManager.MarkSceneDirty(scene);
                Assert.That(EditorSceneManager.SaveScene(scene), Is.True);
            });

            var backup = SaveBackup(store, original.Id);
            var expectedRestore = Guid.NewGuid();
            Track(expectedRestore.ToString("N"));
            var restored = new GenerationStore(() => expectedRestore).Restore(backup);
            Assert.That(restored.Id, Is.EqualTo(expectedRestore.ToString("N")));
            Assert.That(restored.Manifest.parentVariantId, Is.EqualTo(original.Id));
            Assert.That(AssetDatabase.AssetPathToGUID(restored.ScenePath), Is.Not.EqualTo(AssetDatabase.AssetPathToGUID(original.ScenePath)));
            Assert.That(AssetDatabase.AssetPathToGUID(restored.SettingsPath), Is.Not.EqualTo(AssetDatabase.AssetPathToGUID(original.SettingsPath)));
            Assert.That(AssetDatabase.LoadAssetAtPath<RunnerSettings>(restored.SettingsPath).Seed, Is.EqualTo(42));
            WithScene(restored.ScenePath, scene => Assert.That(scene.GetRootGameObjects()[0].name, Is.EqualTo("Caméra modifiée")));
            WithScene(original.ScenePath, scene => Assert.That(scene.GetRootGameObjects()[0].name, Is.EqualTo("Caméra modifiée")));
        }

        [Test]
        public void CorruptBackupIsRejectedBeforeStaging()
        {
            var store = new GenerationStore();
            var variant = Publish(store);
            var backup = SaveBackup(store, variant.Id);
            File.AppendAllText(Path.Combine(BackupPath(backup), "RunnerSettings.asset"), "\ncorrupt");
            var next = Guid.NewGuid();
            var isolatedStore = new GenerationStore(() => next);

            Assert.Throws<InvalidDataException>(() => isolatedStore.Restore(backup));
            Assert.That(AssetDatabase.IsValidFolder(GenerationStore.GeneratedRoot + "/_staging/" + next.ToString("N")), Is.False);
            Assert.That(store.ReadVariant(variant.Id).Id, Is.EqualTo(variant.Id));
        }

        [Test]
        public void ChangedSharedDependencyIsRejectedBeforeStaging()
        {
            var texturePath = "Assets/GameForge/Tests/EditMode/Storage/Texture-" + Guid.NewGuid().ToString("N") + ".renderTexture";
            sharedAssets.Add(texturePath);
            var texture = new RenderTexture(32, 32, 0);
            AssetDatabase.CreateAsset(texture, texturePath);
            var store = new GenerationStore();
            var variant = Publish(store, texture);
            var backup = SaveBackup(store, variant.Id);
            texture.width = 64;
            EditorUtility.SetDirty(texture);
            AssetDatabase.SaveAssetIfDirty(texture);

            var next = Guid.NewGuid();
            var isolatedStore = new GenerationStore(() => next);
            var error = Assert.Throws<InvalidDataException>(() => isolatedStore.Restore(backup));
            Assert.That(error.Message, Does.Contain(texturePath));
            Assert.That(AssetDatabase.IsValidFolder(GenerationStore.GeneratedRoot + "/_staging/" + next.ToString("N")), Is.False);
        }

        private GenerationTransaction Begin(GenerationStore store)
        {
            var transaction = store.Begin();
            Track(transaction.Id);
            return transaction;
        }

        private void Track(string id)
        {
            generated.Add(GenerationStore.GeneratedRoot + "/" + id);
            generated.Add(GenerationStore.GeneratedRoot + "/_staging/" + id);
        }

        private StoredVariant Publish(GenerationStore store, RenderTexture texture = null)
        {
            using var transaction = Begin(store);
            WriteVariant(transaction, texture);
            return transaction.Commit("Original");
        }

        private string SaveBackup(GenerationStore store, string id)
        {
            var backupId = store.Backup(id);
            backups.Add(backupId);
            return backupId;
        }

        private static void WriteVariant(GenerationTransaction transaction, RenderTexture texture = null)
        {
            WriteScene(transaction.ScenePath, texture);
            var config = RunnerConfigV1.CreateDefault();
            config.seed = 42;
            Assert.That(RunnerConfigValidator.TryCreateSettings(config, new FixtureCatalog(), out var settings, out _), Is.True);
            AssetDatabase.CreateAsset(settings, transaction.SettingsPath);
            AssetDatabase.SaveAssetIfDirty(settings);
        }

        private static void WriteScene(string path, RenderTexture texture = null)
        {
            var previous = SceneManager.GetActiveScene();
            if (Application.isBatchMode && SceneManager.sceneCount == 1 && previous.IsValid() &&
                !previous.isDirty && string.IsNullOrEmpty(previous.path))
            {
                previous = EditorSceneManager.OpenScene("Assets/GameForge/Setup/Smoke.unity", OpenSceneMode.Single);
            }
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                var root = new GameObject("Caméra initiale");
                SceneManager.MoveGameObjectToScene(root, scene);
                root.AddComponent<Camera>().targetTexture = texture;
                Assert.That(EditorSceneManager.SaveScene(scene, path), Is.True);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
                if (previous.IsValid())
                    SceneManager.SetActiveScene(previous);
            }
        }

        private static void WithScene(string path, Action<Scene> inspect)
        {
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try { inspect(scene); }
            finally { EditorSceneManager.CloseScene(scene, true); }
        }

        private static string BackupPath(string id) => FullPath("Backups/GameForge/" + id);
        private static string FullPath(string path) => Path.GetFullPath(Path.Combine(Application.dataPath, "..", path));

        private sealed class FixtureCatalog : IRunnerCatalog
        {
            public bool HasTheme(string id) => id == "theme-default";
            public bool HasObstacle(string id) => id == "obstacle-block";
        }
    }
}
