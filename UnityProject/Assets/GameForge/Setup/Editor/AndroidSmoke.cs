using System;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace GameForge.Setup
{
    public static class AndroidSmoke
    {
        public static void Build()
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android ||
                !BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
            {
                throw new InvalidOperationException("Start Unity with -buildTarget Android and install Android Build Support.");
            }

            var scene = "Assets/GameForge/Setup/Smoke.unity";
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scene) == null)
            {
                throw new InvalidOperationException($"Missing smoke scene: {scene}");
            }

            var run = DateTime.UtcNow.ToString("yyyyMMdd-HHmmssfff", CultureInfo.InvariantCulture);
            var folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", ".local", "004", run));
            if (Directory.Exists(folder))
            {
                throw new IOException($"Smoke output already exists: {folder}");
            }

            Directory.CreateDirectory(folder);
            var apkPath = Path.Combine(folder, "android-smoke.apk");
            var target = NamedBuildTarget.Android;
            var previousBackend = PlayerSettings.GetScriptingBackend(target);
            var previousIdentifier = PlayerSettings.GetApplicationIdentifier(target);
            var previousArchitectures = PlayerSettings.Android.targetArchitectures;
            var previousSdk = PlayerSettings.Android.targetSdkVersion;
            var previousKeystore = PlayerSettings.Android.useCustomKeystore;
            var previousBundle = EditorUserBuildSettings.buildAppBundle;
            var previousExport = EditorUserBuildSettings.exportAsGoogleAndroidProject;

            try
            {
                PlayerSettings.SetScriptingBackend(target, ScriptingImplementation.IL2CPP);
                PlayerSettings.SetApplicationIdentifier(target, "com.gameforge.androidsmoke");
                PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
                PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevel36;
                PlayerSettings.Android.useCustomKeystore = false;
                EditorUserBuildSettings.buildAppBundle = false;
                EditorUserBuildSettings.exportAsGoogleAndroidProject = false;

                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { scene },
                    target = BuildTarget.Android,
                    locationPathName = apkPath,
                    options = BuildOptions.Development,
                });

                if (report.summary.result != BuildResult.Succeeded || !File.Exists(apkPath))
                {
                    throw new InvalidOperationException($"Android smoke build: {report.summary.result}, " +
                                                        $"{report.summary.totalErrors} errors.");
                }

                Debug.Log($"GameForge Android smoke APK: {apkPath}");
            }
            finally
            {
                PlayerSettings.SetScriptingBackend(target, previousBackend);
                PlayerSettings.SetApplicationIdentifier(target, previousIdentifier);
                PlayerSettings.Android.targetArchitectures = previousArchitectures;
                PlayerSettings.Android.targetSdkVersion = previousSdk;
                PlayerSettings.Android.useCustomKeystore = previousKeystore;
                EditorUserBuildSettings.buildAppBundle = previousBundle;
                EditorUserBuildSettings.exportAsGoogleAndroidProject = previousExport;
            }
        }
    }
}
