using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using GameForge.Runtime;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Profile;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;

[assembly: InternalsVisibleTo("GameForge.Tests.EditMode")]

namespace GameForge.Editor
{
    public static class AndroidApkExport
    {
        public const string MenuPath = "Tools/Exporter un APK GameForge";
        public const string ProfilePath = "Assets/GameForge/BuildProfiles/AndroidTest.asset";
        public const string VariantArgument = "-gameForgeVariant";
        private const string ApkName = "gameforge-android-test.apk";
        private const string JournalName = ".android-settings-journal.json";

        private static string BuildRoot => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Builds", "GameForge"));

        [MenuItem(MenuPath)]
        public static void ExportFromMenu()
        {
            try
            {
                var id = VariantIdFromPath(AssetDatabase.GetAssetPath(Selection.activeObject)) ??
                    VariantIdFromPath(SceneManager.GetActiveScene().path);
                if (id == null)
                    throw new ArgumentException("Sélectionnez une variante GameForge dans le projet, ou ouvrez sa scène.");

                var report = Export(id);
                if (!Application.isBatchMode)
                    EditorUtility.DisplayDialog("APK GameForge exporté", report.ApkPath, "OK");
            }
            catch (Exception exception) when (!Application.isBatchMode)
            {
                EditorUtility.DisplayDialog("Export APK GameForge", exception.Message, "OK");
            }
        }

        public static void ExportBatch() => Export(ParseBatchId(Environment.GetCommandLineArgs()));

        public static AndroidExportReport Export(string variantId)
        {
            RequireAndroidTarget();
            using var lease = AcquireBuildLock();
            var journal = Path.Combine(BuildRoot, JournalName);
            RequireRegularPath(journal);
            AndroidBuildJournal.Recover(journal, AndroidBuildSettings.Capture, state => state.Apply());
            var variant = new GenerationStore().ReadVariant(variantId);
            var profile = AssetDatabase.LoadAssetAtPath<AndroidTestProfile>(ProfilePath);
            if (profile == null || EditorUtility.IsDirty(profile))
                throw new InvalidDataException("Le profil Android Test doit être présent et enregistré.");

            var report = Resolve(variant, profile, out var settings);
            var directory = NewBuildDirectory(BuildRoot, variant.Id);
            report.buildId = Path.GetFileName(directory);
            report.apkPath = Path.Combine(directory, ApkName);
            var reportPath = Path.Combine(directory, "report.json");
            try
            {
                var toolchain = AndroidToolchain.Resolve(report);
                AndroidBuildJournal.Run(journal, settings, AndroidBuildSettings.Capture, state => state.Apply(),
                    () => BuildPlayer(report));
                toolchain.CheckApk(report);
                report.result = "Succeeded";
            }
            catch (Exception exception)
            {
                report.result ??= "Failed";
                report.diagnostic = exception.Message;
                throw;
            }
            finally
            {
                using var output = new FileStream(reportPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
                using var writer = new StreamWriter(output);
                writer.Write(JsonUtility.ToJson(report, true));
            }

            Debug.Log($"GameForge APK : {report.ApkPath}");
            return report;
        }

        internal static AndroidExportReport Resolve(StoredVariant variant, AndroidTestProfile profile,
            out AndroidBuildSettings settings)
        {
            var scene = SceneManager.GetSceneByPath(variant.ScenePath);
            var asset = AssetDatabase.LoadAssetAtPath<RunnerSettings>(variant.SettingsPath);
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(variant.ScenePath) == null || asset == null ||
                !AssetDatabase.GetDependencies(variant.ScenePath, false).Contains(variant.SettingsPath, StringComparer.Ordinal))
                throw new InvalidDataException("La scène de la variante ou ses réglages GameForge sont invalides.");
            if ((scene.IsValid() && scene.isDirty) || EditorUtility.IsDirty(asset))
                throw new InvalidOperationException("Enregistrez la scène et ses réglages avant d’exporter l’APK.");

            settings = profile.Resolve(ApplicationId(variant.Id), variant.Manifest.title);
            return new AndroidExportReport
            {
                variantId = variant.Id,
                applicationId = settings.applicationId,
                title = settings.title,
                scene = variant.ScenePath,
                profile = ProfilePath,
                unity = Application.unityVersion,
                minimumSdk = (int)settings.minimumSdk,
                targetSdk = (int)settings.targetSdk,
                versionName = settings.versionName,
                versionCode = settings.versionCode,
                architecture = settings.architecture.ToString(),
                backend = settings.backend.ToString(),
                orientation = settings.orientation.ToString(),
                graphicsApi = settings.graphicsApis.Single().ToString()
            };
        }

        private static void BuildPlayer(AndroidExportReport report)
        {
            var result = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                target = BuildTarget.Android,
                scenes = new[] { report.scene },
                locationPathName = report.apkPath,
                options = BuildOptions.Development
            }).summary;
            if (result.result == BuildResult.Cancelled)
            {
                report.result = "Cancelled";
                throw new OperationCanceledException("Export APK annulé.");
            }
            if (result.result != BuildResult.Succeeded || !File.Exists(report.apkPath))
                throw new InvalidOperationException($"Échec de l’export Android ({result.totalErrors} erreur(s)). Consultez le journal Unity.");
        }

        private static void RequireAndroidTarget()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || BuildPipeline.isBuildingPlayer)
                throw new InvalidOperationException("Revenez en mode édition et attendez la fin des compilations Unity.");
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android ||
                !BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android) ||
                BuildProfile.GetActiveBuildProfile() != null)
                throw new InvalidOperationException(
                    "Activez Android dans la liste Platforms de Build Profiles, avec le module Android Build Support.");
        }

        private static FileStream AcquireBuildLock()
        {
            var path = Path.Combine(BuildRoot, ".export.lock");
            RequireRegularPath(path);
            Directory.CreateDirectory(BuildRoot);
            try
            {
                return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException exception)
            {
                throw new IOException("Un export Android est déjà en cours ou son verrou est inaccessible.", exception);
            }
        }

        internal static string NewBuildDirectory(string root, string variantId)
        {
            RequireId(variantId);
            var buildId = DateTime.UtcNow.ToString("yyyyMMdd-HHmmssfff", CultureInfo.InvariantCulture)
                + "-" + Guid.NewGuid().ToString("N");
            var path = Path.Combine(root, variantId, buildId);
            RequireRegularPath(path);
            if (Directory.Exists(path) || File.Exists(path))
                throw new IOException("Le dossier de cet export Android existe déjà.");
            Directory.CreateDirectory(path);
            return path;
        }

        private static void RequireRegularPath(string path)
        {
            for (var parent = path; !string.IsNullOrEmpty(parent); parent = Path.GetDirectoryName(parent))
            {
                try
                {
                    if ((File.GetAttributes(parent) & FileAttributes.ReparsePoint) != 0)
                        throw new IOException("Les liens et jonctions sont interdits dans les dossiers d’export GameForge.");
                }
                catch (FileNotFoundException) { }
                catch (DirectoryNotFoundException) { }
            }
        }

        internal static string VariantIdFromPath(string path)
        {
            const string prefix = GenerationStore.GeneratedRoot + "/";
            if (string.IsNullOrEmpty(path) || !path.StartsWith(prefix, StringComparison.Ordinal))
                return null;
            var id = path.Substring(prefix.Length).Split('/')[0];
            return IsId(id) ? id : null;
        }

        internal static string ParseBatchId(string[] args)
        {
            var index = Array.IndexOf(args, VariantArgument);
            if (index < 0 || index + 1 >= args.Length || Array.LastIndexOf(args, VariantArgument) != index)
                throw new ArgumentException($"Indiquez une variante GameForge avec {VariantArgument} <id>.");
            RequireId(args[index + 1]);
            return args[index + 1];
        }

        internal static string ApplicationId(string id)
        {
            RequireId(id);
            return "com.gameforge.test.v" + id;
        }

        private static void RequireId(string id)
        {
            if (!IsId(id))
                throw new ArgumentException("Identifiant de variante GameForge invalide.", nameof(id));
        }

        private static bool IsId(string id) => Guid.TryParseExact(id, "N", out var guid)
            && guid != Guid.Empty && guid.ToString("N") == id;
    }
}
