using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using GameForge.Runtime;
using UnityEditor;
using UnityEngine;

namespace GameForge.Editor
{
    public sealed partial class GenerationStore : IGenerationStore
    {
        public const string GeneratedRoot = "Assets/GameForgeGenerated";
        internal const string SceneFile = "Runner.unity";
        internal const string SettingsFile = "RunnerSettings.asset";
        private const string ManifestFile = "GameForgeVariant.json";
        private const string OwnerFile = ".gameforge-owner";
        private const int MetadataLimit = 256 * 1024;
        private const int AssetLimit = 64 * 1024 * 1024;
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);
        private static readonly string ProjectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        private static readonly string LockRoot = Path.GetFullPath(Path.Combine(ProjectRoot, "..", ".local", "storage"));

        private readonly Func<Guid> createId;

        public GenerationStore(Func<Guid> createId = null)
        {
            this.createId = createId ?? Guid.NewGuid;
        }

        public GenerationTransaction Begin()
        {
            var lease = AcquireLock();
            try
            {
                return BeginLocked(lease);
            }
            catch
            {
                lease.Dispose();
                throw;
            }
        }

        public StoredVariant ReadVariant(string id)
        {
            RequireId(id);
            var path = GeneratedRoot + "/" + id;
            var directory = ProjectPath(path);
            RequireOwner(directory, id);
            ValidateVariantFiles(directory, includeManifest: true);
            ReadSettings(path);
            var manifest = ReadJson<VariantManifest>(Path.Combine(directory, ManifestFile));
            ValidateManifest(id, manifest);
            return new StoredVariant(path, manifest);
        }

        public StoredVariant Rename(string id, string title)
        {
            RequireTitle(title);
            using var lease = AcquireLock();
            var variant = ReadVariant(id);
            if (variant.Manifest.title == title)
                return variant;

            variant.Manifest.title = title;
            var manifestPath = GeneratedRoot + "/" + id + "/" + ManifestFile;
            var destination = ProjectPath(manifestPath);
            var pending = Path.Combine(LockRoot, Guid.NewGuid().ToString("N") + ".rename");
            WriteNew(pending, JsonUtility.ToJson(variant.Manifest, true));
            AssetDatabase.DisallowAutoRefresh();
            try
            {
                AssetDatabase.ReleaseCachedFileHandles();
                for (var delay = 25; ; delay *= 2)
                {
                    try
                    {
                        File.Replace(pending, destination, null);
                        break;
                    }
                    catch (IOException exception) when (delay <= 200 && Application.platform == RuntimePlatform.WindowsEditor &&
                        (exception.HResult & 0xffff) is 32 or 33 or 1175)
                    {
                        // An import worker can still have the previous manifest open.
                        Thread.Sleep(delay);
                    }
                }
            }
            finally
            {
                AssetDatabase.AllowAutoRefresh();
                File.Delete(pending);
            }

            AssetDatabase.ImportAsset(manifestPath, ImportAssetOptions.ForceSynchronousImport);
            return variant;
        }

        public string[] ListInterrupted()
        {
            using var lease = AcquireLock();
            var staging = ProjectPath(GeneratedRoot + "/_staging");
            if (!Directory.Exists(staging))
                return Array.Empty<string>();

            return Directory.EnumerateDirectories(staging).Take(256)
                .Where(IsOwnedStagingDirectory)
                .Select(Path.GetFileName).ToArray();
        }

        private static bool IsOwnedStagingDirectory(string path)
        {
            try
            {
                var id = Path.GetFileName(path);
                RequireId(id);
                return ReadText(Path.Combine(path, OwnerFile), 64) == id;
            }
            catch (IOException) { return false; }
            catch (ArgumentException) { return false; }
            catch (UnauthorizedAccessException) { return false; }
        }

        internal StoredVariant Publish(GenerationTransaction transaction, string title, string parentVariantId)
        {
            if (parentVariantId != null)
                RequireId(parentVariantId);
            RequireTitle(title);
            var directory = ProjectPath(transaction.StagingPath);
            RequireOwner(directory, transaction.Id);
            ValidateVariantFiles(directory, includeManifest: false);
            var config = ReadSettings(transaction.StagingPath);
            ReadSharedDependencies(transaction.StagingPath);

            var manifest = new VariantManifest
            {
                schemaVersion = RunnerConfigV1.Version,
                generatorVersion = 1,
                variantId = transaction.Id,
                parentVariantId = parentVariantId,
                title = title,
                scene = SceneFile,
                settings = SettingsFile,
                seed = config.seed,
                themeId = config.themeId,
                obstacleIds = config.obstacleIds
            };
            WriteNew(Path.Combine(directory, ManifestFile), JsonUtility.ToJson(manifest, true));
            AssetDatabase.ImportAsset(transaction.StagingPath + "/" + ManifestFile, ImportAssetOptions.ForceSynchronousImport);

            var publishedPath = GeneratedRoot + "/" + transaction.Id;
            RequireUnused(ProjectPath(publishedPath));
            RequireOwner(directory, transaction.Id);
            ValidateVariantFiles(directory, includeManifest: true);
            var error = AssetDatabase.MoveAsset(transaction.StagingPath, publishedPath);
            if (!string.IsNullOrEmpty(error))
                throw new IOException($"Publication Unity impossible. {error}");

            return new StoredVariant(publishedPath, manifest);
        }

        private GenerationTransaction BeginLocked(FileStream lease)
        {
            var id = createId().ToString("N");
            RequireId(id);
            RequireUnused(ProjectPath(GeneratedRoot + "/" + id));
            var path = GeneratedRoot + "/_staging/" + id;
            var directory = ProjectPath(path);
            RequireUnused(directory);
            Directory.CreateDirectory(directory);
            WriteNew(Path.Combine(directory, OwnerFile), id);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            return new GenerationTransaction(this, lease, id, path);
        }

        private static RunnerConfigV1 ReadSettings(string path)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path + "/" + SceneFile) == null ||
                !(AssetDatabase.LoadAssetAtPath<RunnerSettings>(path + "/" + SettingsFile) is RunnerSettings settings))
            {
                throw new InvalidDataException("Une scène Unity et un asset RunnerSettings valides sont requis.");
            }

            RunnerConfigV1 configuration;
            try
            {
                configuration = settings.ToConfig();
            }
            catch (NullReferenceException exception)
            {
                throw new InvalidDataException("L’asset RunnerSettings est incomplet.", exception);
            }

            if (configuration.schemaVersion != RunnerConfigV1.Version ||
                configuration.templateId != RunnerConfigV1.RunnerTemplateId || configuration.obstacleIds == null)
            {
                throw new InvalidDataException("L’asset RunnerSettings ne respecte pas le schéma V1.");
            }

            return configuration;
        }

        private static void ValidateManifest(string id, VariantManifest manifest)
        {
            RequireTitle(manifest?.title);
            if (manifest.variantId != id || manifest.schemaVersion != RunnerConfigV1.Version ||
                manifest.generatorVersion != 1 || manifest.scene != SceneFile || manifest.settings != SettingsFile ||
                manifest.obstacleIds == null)
            {
                throw new InvalidDataException("Manifeste de variante invalide.");
            }
            if (manifest.parentVariantId != null && manifest.parentVariantId.Length > 0)
                RequireId(manifest.parentVariantId);
        }

        private static void RequireTitle(string title)
        {
            if (string.IsNullOrWhiteSpace(title) || title.Length > RunnerConfigValidator.MaxTitleLength ||
                title.Any(char.IsControl))
            {
                throw new InvalidDataException("Titre de variante invalide.");
            }
        }

        private static void RequireId(string id)
        {
            if (!Guid.TryParseExact(id, "N", out var value) || value == Guid.Empty || value.ToString("N") != id)
                throw new ArgumentException("Identifiant de variante ou de sauvegarde invalide.", nameof(id));
        }

        private static void RequireOwner(string directory, string id)
        {
            if (ReadText(Path.Combine(directory, OwnerFile), 64) != id)
                throw new InvalidDataException($"Le dossier {id} n’appartient pas à GameForge.");
            RequireRegularPath(directory + ".meta");
            if (!File.Exists(directory + ".meta"))
                throw new InvalidDataException("Métadonnées du dossier Unity manquantes.");
        }

        private static void ValidateVariantFiles(string directory, bool includeManifest)
        {
            ValidateFiles(directory, OwnerFile, SceneFile, SceneFile + ".meta", SettingsFile,
                SettingsFile + ".meta", includeManifest ? ManifestFile : null,
                includeManifest ? ManifestFile + ".meta" : null);
            foreach (var name in AssetFiles)
            {
                if (!File.Exists(Path.Combine(directory, name)))
                    throw new InvalidDataException($"Fichier de variante manquant. {name}");
            }
        }

        private static void ValidateFiles(string directory, params string[] allowedFiles)
        {
            RequireRegularPath(directory);
            foreach (var path in Directory.EnumerateFileSystemEntries(directory))
            {
                RequireRegularPath(path);
                if (Directory.Exists(path) || !allowedFiles.Contains(Path.GetFileName(path), StringComparer.Ordinal))
                    throw new InvalidDataException($"Fichier inattendu dans la variante ou la sauvegarde. {Path.GetFileName(path)}");
            }
        }

        private static FileStream AcquireLock()
        {
            RequireRegularPath(LockRoot);
            Directory.CreateDirectory(LockRoot);
            var path = Path.Combine(LockRoot, "generation.lock");
            RequireRegularPath(path);
            try
            {
                return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException exception)
            {
                throw new IOException("Une autre opération GameForge est active ou le verrou est inaccessible.", exception);
            }
        }

        private static string ProjectPath(string relative)
        {
            var full = Path.GetFullPath(Path.Combine(ProjectRoot, relative));
            if (!full.StartsWith(ProjectRoot + Path.DirectorySeparatorChar, PathComparison))
                throw new IOException("Chemin hors du projet Unity.");
            RequireRegularPath(full);
            return full;
        }

        private static StringComparison PathComparison => Application.platform == RuntimePlatform.WindowsEditor
            ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

        private static void RequireRegularPath(string full)
        {
            for (var path = full; !string.IsNullOrEmpty(path); path = Path.GetDirectoryName(path))
            {
                var attributes = Attributes(path);
                if (attributes.HasValue && (attributes.Value & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("Les liens et jonctions sont interdits dans les chemins GameForge.");
            }
        }

        private static void RequireUnused(string path)
        {
            RequireRegularPath(path);
            RequireRegularPath(path + ".meta");
            if (Attributes(path).HasValue || Attributes(path + ".meta").HasValue)
                throw new IOException($"Un dossier GameForge ou son fichier .meta existe déjà. {Path.GetFileName(path)}");
        }

        private static FileAttributes? Attributes(string path)
        {
            try { return File.GetAttributes(path); }
            catch (FileNotFoundException) { return null; }
            catch (DirectoryNotFoundException) { return null; }
        }

        private static string ReadText(string path, int limit)
        {
            RequireRegularPath(path);
            using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (input.Length > limit)
                throw new InvalidDataException($"Fichier GameForge trop volumineux. {Path.GetFileName(path)}");
            using var reader = new StreamReader(input, Utf8);
            return reader.ReadToEnd();
        }

        private static T ReadJson<T>(string path) where T : class =>
            JsonUtility.FromJson<T>(ReadText(path, MetadataLimit)) ?? throw new InvalidDataException("Métadonnées GameForge manquantes.");

        private static void WriteNew(string path, string contents)
        {
            RequireRegularPath(path);
            using var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            using var writer = new StreamWriter(output, Utf8);
            writer.Write(contents);
        }
    }
}
