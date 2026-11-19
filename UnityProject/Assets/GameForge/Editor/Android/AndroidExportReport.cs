using System;

namespace GameForge.Editor
{
    [Serializable]
    public sealed class AndroidExportReport
    {
        public int schemaVersion = 1;
        public string variantId;
        public string buildId;
        public string applicationId;
        public string title;
        public string scene;
        public string profile;
        public string unity;
        public int minimumSdk;
        public int targetSdk;
        public string sdkPlatformRevision;
        public string sdkBuildTools;
        public string ndk;
        public string jdk;
        public string gradle;
        public string architecture;
        public string backend;
        public string orientation;
        public string graphicsApi;
        public string launcherIcon = "Projet Unity";
        public string signing = "Android Debug";
        public string versionName;
        public int versionCode;
        public string apkPath;
        public string apkSha256;
        public string signerSha256;
        public long apkBytes;
        public string result;
        public string diagnostic;

        public string ApkPath => apkPath;
    }
}
