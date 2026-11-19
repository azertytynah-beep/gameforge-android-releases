using System;
using System.IO;

namespace GameForge.Editor
{
    public interface IGenerationStore
    {
        GenerationTransaction Begin();
    }

    [Serializable]
    public sealed class VariantManifest
    {
        public int schemaVersion;
        public int generatorVersion;
        public string variantId;
        public string parentVariantId;
        public string title;
        public string scene;
        public string settings;
        public int seed;
        public string themeId;
        public string[] obstacleIds;
    }

    public sealed class StoredVariant
    {
        internal StoredVariant(string path, VariantManifest manifest)
        {
            Id = manifest.variantId;
            Manifest = manifest;
            ScenePath = path + "/" + manifest.scene;
            SettingsPath = path + "/" + manifest.settings;
        }

        public string Id { get; }
        public string ScenePath { get; }
        public string SettingsPath { get; }
        public VariantManifest Manifest { get; }
    }

    public sealed class GenerationTransaction : IDisposable
    {
        private readonly GenerationStore store;
        private FileStream lease;

        internal GenerationTransaction(GenerationStore store, FileStream lease, string id, string stagingPath)
        {
            this.store = store;
            this.lease = lease;
            Id = id;
            StagingPath = stagingPath;
        }

        public string Id { get; }
        public string StagingPath { get; }
        public string ScenePath => StagingPath + "/" + GenerationStore.SceneFile;
        public string SettingsPath => StagingPath + "/" + GenerationStore.SettingsFile;

        public StoredVariant Commit(string title, string parentVariantId = null)
        {
            if (lease == null)
                throw new ObjectDisposedException(nameof(GenerationTransaction));

            var result = store.Publish(this, title, parentVariantId);
            Dispose();
            return result;
        }

        public void Abort() => Dispose();

        // Le dossier de staging reste disponible pour le diagnostic après abandon ou arrêt.
        public void Dispose()
        {
            lease?.Dispose();
            lease = null;
        }
    }
}
