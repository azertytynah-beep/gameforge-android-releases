using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Rendering;

namespace GameForge.Editor
{
    internal sealed class AndroidBuildSettings
    {
        private const BuildTarget Target = BuildTarget.Android;
        private const string IdentifierOverride = "overrideDefaultApplicationIdentifier";
        private static readonly MethodInfo GetProjectSettings = typeof(PlayerSettings).GetMethod(
            "GetProjectSettingsPlayerSettings", BindingFlags.NonPublic | BindingFlags.Static);

        public string applicationId;
        public bool overrideDefaultApplicationIdentifier;
        public string title;
        public string versionName;
        public int versionCode;
        public AndroidSdkVersions minimumSdk;
        public AndroidSdkVersions targetSdk;
        public UIOrientation orientation;
        public ScriptingImplementation backend;
        public AndroidArchitecture architecture;
        public bool useCustomKeystore;
        public bool splitApplicationBinary;
        public bool buildApkPerCpuArchitecture;
        public bool minifyDebug;
        public bool buildAppBundle;
        public bool exportGradleProject;
        public bool automaticGraphicsApis;
        public GraphicsDeviceType[] graphicsApis;

        public static AndroidBuildSettings Capture() => new AndroidBuildSettings
        {
            applicationId = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android),
            overrideDefaultApplicationIdentifier = ReadIdentifierOverride(),
            title = PlayerSettings.productName,
            versionName = PlayerSettings.bundleVersion,
            versionCode = PlayerSettings.Android.bundleVersionCode,
            minimumSdk = PlayerSettings.Android.minSdkVersion,
            targetSdk = PlayerSettings.Android.targetSdkVersion,
            orientation = PlayerSettings.defaultInterfaceOrientation,
            backend = PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android),
            architecture = PlayerSettings.Android.targetArchitectures,
            useCustomKeystore = PlayerSettings.Android.useCustomKeystore,
            splitApplicationBinary = PlayerSettings.Android.splitApplicationBinary,
            buildApkPerCpuArchitecture = PlayerSettings.Android.buildApkPerCpuArchitecture,
            minifyDebug = PlayerSettings.Android.minifyDebug,
            buildAppBundle = EditorUserBuildSettings.buildAppBundle,
            exportGradleProject = EditorUserBuildSettings.exportAsGoogleAndroidProject,
            automaticGraphicsApis = PlayerSettings.GetUseDefaultGraphicsAPIs(Target),
            graphicsApis = PlayerSettings.GetGraphicsAPIs(Target)
        };

        public void Apply()
        {
            var current = Capture();
            if (current.applicationId != applicationId)
                PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, applicationId);
            if (current.title != title)
                PlayerSettings.productName = title;
            if (current.versionName != versionName)
                PlayerSettings.bundleVersion = versionName;
            if (current.versionCode != versionCode)
                PlayerSettings.Android.bundleVersionCode = versionCode;
            if (current.minimumSdk != minimumSdk)
                PlayerSettings.Android.minSdkVersion = minimumSdk;
            if (current.targetSdk != targetSdk)
                PlayerSettings.Android.targetSdkVersion = targetSdk;
            if (current.orientation != orientation)
                PlayerSettings.defaultInterfaceOrientation = orientation;
            if (current.backend != backend)
                PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, backend);
            if (current.architecture != architecture)
                PlayerSettings.Android.targetArchitectures = architecture;
            if (current.useCustomKeystore != useCustomKeystore)
                PlayerSettings.Android.useCustomKeystore = useCustomKeystore;
            if (current.splitApplicationBinary != splitApplicationBinary)
                PlayerSettings.Android.splitApplicationBinary = splitApplicationBinary;
            if (current.buildApkPerCpuArchitecture != buildApkPerCpuArchitecture)
                PlayerSettings.Android.buildApkPerCpuArchitecture = buildApkPerCpuArchitecture;
            if (current.minifyDebug != minifyDebug)
                PlayerSettings.Android.minifyDebug = minifyDebug;
            if (current.buildAppBundle != buildAppBundle)
                EditorUserBuildSettings.buildAppBundle = buildAppBundle;
            if (current.exportGradleProject != exportGradleProject)
                EditorUserBuildSettings.exportAsGoogleAndroidProject = exportGradleProject;
            if (current.automaticGraphicsApis && !automaticGraphicsApis)
                PlayerSettings.SetUseDefaultGraphicsAPIs(Target, false);
            if (!PlayerSettings.GetGraphicsAPIs(Target).SequenceEqual(graphicsApis))
                PlayerSettings.SetGraphicsAPIs(Target, graphicsApis);
            if (PlayerSettings.GetUseDefaultGraphicsAPIs(Target) != automaticGraphicsApis)
                PlayerSettings.SetUseDefaultGraphicsAPIs(Target, automaticGraphicsApis);
            if (ReadIdentifierOverride() != overrideDefaultApplicationIdentifier)
                WriteIdentifierOverride(overrideDefaultApplicationIdentifier);
        }

        private static bool ReadIdentifierOverride()
        {
            using var settings = ProjectSettingsObject();
            using var property = FindIdentifierOverride(settings);
            return property.boolValue;
        }

        private static void WriteIdentifierOverride(bool value)
        {
            using var settings = ProjectSettingsObject();
            using var property = FindIdentifierOverride(settings);
            property.boolValue = value;
            settings.ApplyModifiedPropertiesWithoutUndo();
        }

        private static SerializedObject ProjectSettingsObject()
        {
            var settings = GetProjectSettings?.Invoke(null, null) as PlayerSettings
                ?? AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")
                    .OfType<PlayerSettings>().FirstOrDefault();
            if (settings == null)
                throw new InvalidOperationException("Impossible de lire l’option d’identifiant Android du projet.");
            return new SerializedObject(settings);
        }

        // shortcut: Unity 6000.3 ne fournit pas d’API publique pour cette case, réviser à la prochaine version du socle.
        private static SerializedProperty FindIdentifierOverride(SerializedObject settings) =>
            settings.FindProperty(IdentifierOverride) ??
            throw new InvalidOperationException("Option d’identifiant Android introuvable dans les réglages Unity.");
    }
}
