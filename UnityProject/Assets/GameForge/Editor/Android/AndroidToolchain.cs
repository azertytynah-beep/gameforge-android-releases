using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using UnityEditor;
#if UNITY_ANDROID
using UnityEditor.Android;
#endif
using UnityEngine;

namespace GameForge.Editor
{
    internal sealed class AndroidToolchain
    {
        private const string BuildToolsVersion = "36.0.0";
        private readonly string aapt;
        private readonly string java;
        private readonly string apksigner;

        private AndroidToolchain(string aapt, string java, string apksigner)
        {
            this.aapt = aapt;
            this.java = java;
            this.apksigner = apksigner;
        }

        public static AndroidToolchain Resolve(AndroidExportReport report)
        {
#if UNITY_ANDROID
            var engine = BuildPipeline.GetPlaybackEngineDirectory(BuildTarget.Android, BuildOptions.None);
            if (string.IsNullOrEmpty(engine))
                throw new InvalidOperationException("Le module Unity Android Build Support est introuvable.");

            var sdk = ResolvePath(AndroidExternalToolsSettings.sdkRootPath, engine, "SDK");
            var ndk = ResolvePath(AndroidExternalToolsSettings.ndkRootPath, engine, "NDK");
            var jdk = ResolvePath(AndroidExternalToolsSettings.jdkRootPath, engine, "OpenJDK");
            var gradle = ResolvePath(AndroidExternalToolsSettings.Gradle.path, engine, "Tools", "gradle");
            var platform = Path.Combine(sdk, "platforms", $"android-{report.targetSdk}");
            var buildTools = Path.Combine(sdk, "build-tools", BuildToolsVersion);
            var executableExtension = Application.platform == RuntimePlatform.WindowsEditor ? ".exe" : string.Empty;

            RequireFile(Path.Combine(platform, "android.jar"));
            report.sdkPlatformRevision = ReadProperty(Path.Combine(platform, "source.properties"), "Pkg.Revision");
            report.sdkBuildTools = ReadProperty(Path.Combine(buildTools, "source.properties"), "Pkg.Revision");
            report.ndk = ReadProperty(Path.Combine(ndk, "source.properties"), "Pkg.Revision");
            report.jdk = ReadProperty(Path.Combine(jdk, "release"), "JAVA_VERSION");
            var gradleLib = Path.Combine(gradle, "lib");
            if (!Directory.Exists(gradleLib))
                throw new DirectoryNotFoundException("Le dossier lib de Gradle est introuvable dans les outils externes Unity.");
            var launcher = Directory.GetFiles(gradleLib, "gradle-launcher-*.jar");
            if (launcher.Length != 1)
                throw new InvalidDataException("La version de Gradle des outils externes Unity est ambiguë.");
            report.gradle = Path.GetFileNameWithoutExtension(launcher[0]).Substring("gradle-launcher-".Length);
            if (report.ndk != "27.2.12479018" || !report.jdk.StartsWith("17.", StringComparison.Ordinal) ||
                report.sdkBuildTools != BuildToolsVersion)
                throw new InvalidDataException("Le profil APK Android Test attend les outils Unity NDK r27c, JDK 17 et Build Tools 36.0.0.");

            return new AndroidToolchain(
                RequireFile(Path.Combine(buildTools, "aapt" + executableExtension)),
                RequireFile(Path.Combine(jdk, "bin", "java" + executableExtension)),
                RequireFile(Path.Combine(buildTools, "lib", "apksigner.jar")));
#else
            throw new InvalidOperationException("Activez la plateforme Android avant d’utiliser ses outils d’export.");
#endif
        }

        public void CheckApk(AndroidExportReport report)
        {
            var apk = Quote(report.apkPath);
            var badging = Run(aapt, "dump badging " + apk);
            var package = Regex.Match(badging,
                @"(?m)^package: name='([^']+)' versionCode='([^']+)' versionName='([^']+)'");
            if (!package.Success || package.Groups[1].Value != report.applicationId ||
                package.Groups[2].Value != report.versionCode.ToString(System.Globalization.CultureInfo.InvariantCulture) ||
                package.Groups[3].Value != report.versionName ||
                !HasLine(badging, $"sdkVersion:'{report.minimumSdk}'") ||
                !HasLine(badging, $"targetSdkVersion:'{report.targetSdk}'") ||
                !HasLine(badging, "native-code: 'arm64-v8a'"))
                throw new InvalidDataException("Le manifeste ou l’architecture de l’APK ne correspond pas au profil Test.");

            var signing = Run(java, "-jar " + Quote(apksigner) + " verify --verbose --print-certs " + apk);
            var certificate = Regex.Match(signing, @"(?m)^Signer #1 certificate SHA-256 digest: ([a-fA-F0-9]{64})\s*$");
            if (!certificate.Success || !HasLine(signing, "Verified using v2 scheme (APK Signature Scheme v2): true") ||
                !Regex.IsMatch(signing, @"(?m)^Signer #1 certificate DN:.*\bCN=Android Debug(?:,|\s*$)"))
                throw new InvalidDataException("La signature Android Debug de l’APK est invalide.");

            report.signerSha256 = certificate.Groups[1].Value;
            using var input = File.OpenRead(report.apkPath);
            using var sha = SHA256.Create();
            report.apkBytes = input.Length;
            report.apkSha256 = BitConverter.ToString(sha.ComputeHash(input)).Replace("-", string.Empty).ToLowerInvariant();
        }

        private static string ResolvePath(string configured, string engine, params string[] relative) =>
            string.IsNullOrEmpty(configured) ? Path.Combine(new[] { engine }.Concat(relative).ToArray()) : configured;

        private static string ReadProperty(string path, string key)
        {
            foreach (var line in File.ReadLines(RequireFile(path)))
            {
                var parts = line.Split(new[] { '=' }, 2);
                if (parts.Length == 2 && parts[0].Trim() == key)
                    return parts[1].Trim().Trim('"');
            }
            throw new InvalidDataException($"Version {key} absente de {Path.GetFileName(path)}.");
        }

        private static string RequireFile(string path)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException($"Outil Android introuvable. Vérifiez les outils externes Unity : {path}");
            return path;
        }

        private static bool HasLine(string output, string expected) =>
            output.Split('\n').Any(line => line.Trim() == expected);

        private static string Quote(string path)
        {
            if (path.IndexOfAny(new[] { '"', '\r', '\n' }) >= 0)
                throw new ArgumentException("Chemin Android non pris en charge.");
            return "\"" + path + "\"";
        }

        private static string Run(string executable, string arguments)
        {
            using var process = Process.Start(new ProcessStartInfo(executable, arguments)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            }) ?? throw new IOException($"Impossible de lancer {Path.GetFileName(executable)}.");
            var output = process.StandardOutput.ReadToEndAsync();
            var errors = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(60000))
            {
                process.Kill();
                throw new TimeoutException($"Contrôle Android trop long : {Path.GetFileName(executable)}.");
            }
            if (process.ExitCode != 0)
                throw new IOException($"{Path.GetFileName(executable)} n’a pas validé l’APK. {errors.GetAwaiter().GetResult().Trim()}");
            return output.GetAwaiter().GetResult();
        }
    }
}
