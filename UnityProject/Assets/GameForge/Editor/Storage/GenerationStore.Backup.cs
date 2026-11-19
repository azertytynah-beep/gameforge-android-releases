using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using GameForge.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace GameForge.Editor
{
    public sealed partial class GenerationStore
    {
        private const string BackupRoot = "Backups/GameForge";
        private const string BackupFile = "GameForgeBackup.json";
        private static readonly string[] AssetFiles = { SceneFile, SceneFile + ".meta", SettingsFile, SettingsFile + ".meta" };
        private static readonly Regex MetaGuid = new Regex(@"^guid:[ \t]*([a-f0-9]{32})(?=[ \t]*\r?$)", RegexOptions.Multiline);
        private static readonly Regex SerializedGuid = new Regex(@"(?<=\bguid: )([a-f0-9]{32})(?=,\s*type:\s*\d+\s*\})");

        [Serializable]
        private sealed class Dependency
        {
            public string path;
            public string guid;
            public string hash;
        }

        [Serializable]
        private sealed class BackupMetadata
        {
            public int version;
            public string id;
            public string unity;
            public VariantManifest variant;
            public string[] hashes;
            public Dependency[] dependencies;
            public bool sceneUsesSettings;
        }

        public string Backup(string id)
        {
            using var lease = AcquireLock();
            var variant = ReadVariant(id);
            var scene = SceneManager.GetSceneByPath(variant.ScenePath);
            var settings = AssetDatabase.LoadAssetAtPath<RunnerSettings>(variant.SettingsPath);
            if ((scene.IsValid() && scene.isDirty) || EditorUtility.IsDirty(settings))
                throw new InvalidOperationException("Enregistrez la scène et les réglages avant leur sauvegarde.");

            var sourcePath = GeneratedRoot + "/" + id;
            var source = ProjectPath(sourcePath);
            var dependencies = ReadSharedDependencies(sourcePath);
            var usesSettings = SceneUsesSettings(sourcePath);
            var backupId = Guid.NewGuid().ToString("N");
            var staging = ProjectPath(BackupRoot + "/_staging/" + backupId);
            var destination = ProjectPath(BackupRoot + "/" + backupId);
            RequireUnused(destination);
            RequireUnused(staging);
            Directory.CreateDirectory(staging);
            WriteNew(Path.Combine(staging, OwnerFile), backupId);
            var hashes = new string[AssetFiles.Length];
            for (var index = 0; index < AssetFiles.Length; index++)
            {
                var name = AssetFiles[index];
                var copy = Path.Combine(staging, name);
                File.Copy(Path.Combine(source, name), copy);
                hashes[index] = HashAsset(copy);
            }

            var metadata = new BackupMetadata
            {
                version = 1,
                id = backupId,
                unity = Application.unityVersion,
                variant = variant.Manifest,
                hashes = hashes,
                dependencies = dependencies,
                sceneUsesSettings = usesSettings
            };
            WriteNew(Path.Combine(staging, BackupFile), JsonUtility.ToJson(metadata, true));
            ReadBackup(staging, backupId);
            RequireUnused(destination);
            Directory.Move(staging, destination);
            return backupId;
        }

        public StoredVariant Restore(string backupId)
        {
            RequireId(backupId);
            using var lease = AcquireLock();
            var backup = ProjectPath(BackupRoot + "/" + backupId);
            var metadata = ReadBackup(backup, backupId);
            foreach (var dependency in metadata.dependencies)
            {
                var path = IsBuiltIn(dependency.path) ? dependency.path : AssetDatabase.GUIDToAssetPath(dependency.guid);
                if (string.IsNullOrEmpty(path) || !SameDependency(dependency, InspectDependency(path)))
                    throw new InvalidDataException($"Dépendance modifiée ou introuvable. {dependency.path}");
            }

            var sceneYaml = ReadYaml(Path.Combine(backup, SceneFile));
            var settingsYaml = ReadYaml(Path.Combine(backup, SettingsFile));
            var sceneMeta = ReadText(Path.Combine(backup, SceneFile + ".meta"), MetadataLimit);
            var settingsMeta = ReadText(Path.Combine(backup, SettingsFile + ".meta"), MetadataLimit);
            var oldSceneGuid = ReadAssetGuid(sceneMeta);
            var oldSettingsGuid = ReadAssetGuid(settingsMeta);
            if (oldSceneGuid == oldSettingsGuid)
                throw new InvalidDataException("GUID de scène et de réglages identiques.");

            var newSceneGuid = Guid.NewGuid().ToString("N");
            var newSettingsGuid = Guid.NewGuid().ToString("N");
            string Remap(Match match) => match.Value == oldSceneGuid ? newSceneGuid
                : match.Value == oldSettingsGuid ? newSettingsGuid : match.Value;

            using var transaction = BeginLocked(lease);
            var staging = ProjectPath(transaction.StagingPath);
            WriteNew(Path.Combine(staging, SceneFile + ".meta"), MetaGuid.Replace(SerializedGuid.Replace(sceneMeta, Remap),
                "guid: " + newSceneGuid, 1));
            WriteNew(Path.Combine(staging, SettingsFile + ".meta"), MetaGuid.Replace(SerializedGuid.Replace(settingsMeta, Remap),
                "guid: " + newSettingsGuid, 1));
            WriteNew(Path.Combine(staging, SceneFile), SerializedGuid.Replace(sceneYaml, Remap));
            WriteNew(Path.Combine(staging, SettingsFile), SerializedGuid.Replace(settingsYaml, Remap));
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            if (AssetDatabase.AssetPathToGUID(transaction.ScenePath) != newSceneGuid ||
                AssetDatabase.AssetPathToGUID(transaction.SettingsPath) != newSettingsGuid ||
                SceneUsesSettings(transaction.StagingPath) != metadata.sceneUsesSettings)
            {
                throw new InvalidDataException("Les références restaurées vers la scène ou les réglages sont invalides.");
            }

            var restoredDependencies = ReadSharedDependencies(transaction.StagingPath);
            if (restoredDependencies.Length != metadata.dependencies.Length ||
                restoredDependencies.Any(dependency => !metadata.dependencies.Any(expected => SameDependency(expected, dependency))))
            {
                throw new InvalidDataException("Les dépendances de la variante restaurée ne correspondent pas à la sauvegarde.");
            }

            return transaction.Commit(metadata.variant.title, metadata.variant.variantId);
        }

        private static BackupMetadata ReadBackup(string directory, string id)
        {
            ValidateFiles(directory, AssetFiles.Concat(new[] { OwnerFile, BackupFile }).ToArray());
            var metadata = ReadJson<BackupMetadata>(Path.Combine(directory, BackupFile));
            if (ReadText(Path.Combine(directory, OwnerFile), 64) != id || metadata.id != id ||
                metadata.version != 1 || metadata.unity != Application.unityVersion || metadata.variant == null ||
                metadata.hashes?.Length != AssetFiles.Length || metadata.dependencies == null)
            {
                throw new InvalidDataException("Métadonnées de sauvegarde invalides ou version Unity incompatible.");
            }

            RequireId(metadata.variant.variantId);
            ValidateManifest(metadata.variant.variantId, metadata.variant);
            foreach (var dependency in metadata.dependencies)
            {
                if (dependency == null || string.IsNullOrEmpty(dependency.path) ||
                    string.IsNullOrEmpty(dependency.hash))
                {
                    throw new InvalidDataException("Dépendance de sauvegarde invalide.");
                }
                if (!IsBuiltIn(dependency.path))
                    RequireId(dependency.guid);
            }

            for (var index = 0; index < AssetFiles.Length; index++)
            {
                if (metadata.hashes[index] != HashAsset(Path.Combine(directory, AssetFiles[index])))
                    throw new InvalidDataException($"Sauvegarde corrompue. {AssetFiles[index]}");
            }

            return metadata;
        }

        private static string ReadYaml(string path)
        {
            var yaml = ReadText(path, AssetLimit);
            // shortcut: restauration limitée aux scènes et réglages sérialisés en YAML, à étendre avec un format d’export versionné.
            if (!yaml.StartsWith("%YAML", StringComparison.Ordinal))
                throw new InvalidDataException("Les sauvegardes GameForge V1 requièrent des assets Unity en YAML.");
            return yaml;
        }

        private static string ReadAssetGuid(string meta)
        {
            var matches = MetaGuid.Matches(meta);
            if (matches.Count != 1)
                throw new InvalidDataException("GUID d’asset introuvable ou ambigu.");
            return matches[0].Groups[1].Value;
        }

        private static bool SceneUsesSettings(string folder) => AssetDatabase.GetDependencies(folder + "/" + SceneFile, false)
            .Contains(folder + "/" + SettingsFile, StringComparer.Ordinal);

        private static Dependency[] ReadSharedDependencies(string folder)
        {
            var scene = folder + "/" + SceneFile;
            var settings = folder + "/" + SettingsFile;
            ValidateSerializedReferences(scene);
            ValidateSerializedReferences(settings);
            return AssetDatabase.GetDependencies(new[] { scene, settings }, true)
                .Where(path => path != scene && path != settings)
                .OrderBy(path => path, StringComparer.Ordinal)
                .Select(InspectDependency)
                .ToArray();
        }

        private static void ValidateSerializedReferences(string asset)
        {
            foreach (Match match in SerializedGuid.Matches(ReadYaml(ProjectPath(asset))))
            {
                var guid = match.Value;
                if (guid == "00000000000000000000000000000000" || guid == "0000000000000000e000000000000000" ||
                    guid == "0000000000000000f000000000000000")
                {
                    continue;
                }
                if (string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath(guid)))
                    throw new InvalidDataException($"Une référence sérialisée est introuvable dans {Path.GetFileName(asset)}. GUID {guid}.");
            }
        }

        private static Dependency InspectDependency(string path)
        {
            if (path.StartsWith(GeneratedRoot + "/", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"La variante référence un autre asset généré. {path}");

            if (IsBuiltIn(path))
            {
                return new Dependency
                {
                    path = path,
                    guid = string.Empty,
                    hash = Application.unityVersion + ":" + AssetDatabase.GetAssetDependencyHash(path)
                };
            }

            var guid = AssetDatabase.AssetPathToGUID(path);
            RequireId(guid);
            string full;
            if (path.StartsWith("Assets/", StringComparison.Ordinal))
            {
                full = ProjectPath(path);
            }
            else if (path.StartsWith("Packages/", StringComparison.Ordinal))
            {
                var package = PackageInfo.FindForAssetPath(path);
                if (package == null || !path.StartsWith(package.assetPath + "/", StringComparison.Ordinal))
                    throw new InvalidDataException($"Package de dépendance introuvable. {path}");
                var packageRoot = Path.GetFullPath(package.resolvedPath);
                full = Path.GetFullPath(Path.Combine(packageRoot, path.Substring(package.assetPath.Length + 1)));
                if (!full.StartsWith(packageRoot + Path.DirectorySeparatorChar, PathComparison))
                    throw new InvalidDataException("Dépendance de package hors de son dossier.");
            }
            else
            {
                throw new InvalidDataException($"Dépendance Unity non prise en charge. {path}");
            }

            var meta = full + ".meta";
            RequireRegularPath(meta);
            return new Dependency
            {
                path = path,
                guid = guid,
                hash = HashAsset(full) + ":" + (File.Exists(meta) ? HashAsset(meta) : string.Empty)
            };
        }

        private static bool IsBuiltIn(string path) => path == "Resources/unity_builtin_extra" ||
            path == "Library/unity default resources" || path == "Library/unity editor resources";

        private static bool SameDependency(Dependency expected, Dependency actual) => expected.hash == actual.hash &&
            expected.guid == actual.guid && (!IsBuiltIn(expected.path) || expected.path == actual.path);

        private static string HashAsset(string path)
        {
            RequireRegularPath(path);
            using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (file.Length > AssetLimit)
                throw new InvalidDataException($"Asset de sauvegarde trop volumineux. {Path.GetFileName(path)}");
            using var digest = SHA256.Create();
            return BitConverter.ToString(digest.ComputeHash(file)).Replace("-", string.Empty);
        }
    }
}
