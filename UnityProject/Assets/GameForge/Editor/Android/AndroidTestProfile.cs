using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace GameForge.Editor
{
    public sealed class AndroidTestProfile : ScriptableObject
    {
        [SerializeField] private string versionName = "0.1.0";
        [SerializeField] private int versionCode = 1;
        [SerializeField] private AndroidSdkVersions minimumSdk = AndroidSdkVersions.AndroidApiLevel25;
        [SerializeField] private AndroidSdkVersions targetSdk = AndroidSdkVersions.AndroidApiLevel36;
        [SerializeField] private UIOrientation orientation = UIOrientation.Portrait;
        [SerializeField] private GraphicsDeviceType graphicsApi = GraphicsDeviceType.OpenGLES3;

        internal AndroidBuildSettings Resolve(string applicationId, string title)
        {
            if (string.IsNullOrWhiteSpace(versionName) || versionCode < 1 ||
                (int)minimumSdk < 25 || (int)targetSdk < (int)minimumSdk ||
                orientation != UIOrientation.Portrait || graphicsApi != GraphicsDeviceType.OpenGLES3)
                throw new System.IO.InvalidDataException("Le profil APK Android Test est invalide.");

            return new AndroidBuildSettings
            {
                applicationId = applicationId,
                overrideDefaultApplicationIdentifier = true,
                title = title,
                versionName = versionName,
                versionCode = versionCode,
                minimumSdk = minimumSdk,
                targetSdk = targetSdk,
                orientation = orientation,
                backend = ScriptingImplementation.IL2CPP,
                architecture = AndroidArchitecture.ARM64,
                graphicsApis = new[] { graphicsApi }
            };
        }
    }
}
