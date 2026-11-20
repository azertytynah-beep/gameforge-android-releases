using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using GameForge.Editor;
using GameForge.Runtime;
using Microsoft.Win32;

namespace GameForge.Desktop
{
    internal static class Preferences
    {
        private const string Key = @"HKEY_CURRENT_USER\Software\GameForge AI\Studio";
        public static string GetString(string name, string fallback) => (string)Registry.GetValue(Key, name, fallback) ?? fallback;
        public static void SetString(string name, string value) => Registry.SetValue(Key, name, value);
    }

    internal sealed class DesktopCatalog : IRunnerCatalog
    {
        internal sealed class Entry { public string Id; }
        public List<Entry> Themes { get; }
        public List<Entry> Obstacles { get; }

        public DesktopCatalog()
        {
            var ids = File.ReadAllLines(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "catalogue.txt"));
            Themes = Read("theme-");
            Obstacles = Read("obstacle-");
            List<Entry> Read(string prefix) => ids.Where(id => id.StartsWith(prefix, StringComparison.Ordinal))
                .Select(id => new Entry { Id = id }).ToList();
        }
        public bool HasTheme(string id) => Themes.Any(entry => entry.Id == id);
        public bool HasObstacle(string id) => Obstacles.Any(entry => entry.Id == id);
    }

    internal sealed class StudioRuntime
    {
        internal const string UnityVersion = "6000.3.28f1";
        internal readonly ForgeProjectService Projects;
        internal readonly string Root;
        internal string EngineProject => Path.Combine(Root, "UnityProject");
        internal string Log => Path.Combine(Root, "generation.log");
        internal static string DefaultRoot => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GameForge AI", "Atelier");
        internal StudioRuntime(string root)
        {
            Root = Path.GetFullPath(root);
            Projects = new ForgeProjectService(new ForgeProjectStore(Path.Combine(Root, ".local", "projects")), new DesktopCatalog());
        }

        internal string FindEditor()
        {
            var options = new[] { Preferences.GetString("Unity", ""), Path.Combine(Environment.GetFolderPath(
                Environment.SpecialFolder.ProgramFiles), "Unity", "Hub", "Editor", UnityVersion, "Editor", "Unity.exe") };
            return options.FirstOrDefault(IsCompatibleEditor);
        }
        internal static bool IsCompatibleEditor(string file)
        {
            if (!File.Exists(file) || !string.Equals(Path.GetFileName(file), "Unity.exe", StringComparison.OrdinalIgnoreCase))
                return false;
            var version = FileVersionInfo.GetVersionInfo(file).ProductVersion ?? "";
            return version.Split(' ', '(', '+', '_')[0] == UnityVersion;
        }

        internal void PrepareEngine()
        {
            var marker = "Assets/GameForge/Editor/Generation/DesktopGeneration.cs";
            if (File.Exists(Path.Combine(EngineProject, marker))) return;
            if (Directory.Exists(EngineProject))
                throw new IOException("Le dossier du moteur existe déjà et son modèle est incomplet. Les fichiers ont été conservés.");
            Directory.CreateDirectory(Root);
            var incoming = Path.Combine(Root, ".import-" + Guid.NewGuid().ToString("N"));
            ZipFile.ExtractToDirectory(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "modele.zip"), incoming);
            if (!File.Exists(Path.Combine(incoming, marker))) throw new InvalidDataException("Le modèle du Runner est incomplet.");
            Directory.Move(incoming, EngineProject);
        }

        internal async Task<ForgeProject> Generate(string editor, ForgeProject draft, int index)
        {
            if (!IsCompatibleEditor(editor)) throw new InvalidOperationException("Sélectionne Unity " + UnityVersion + " dans les réglages.");
            if (draft.stage != ForgeStage.Gameplay || index < 0 || index >= ForgeProject.ProposalCount)
                throw new InvalidOperationException("Choisis une façon de jouer.");
            PrepareEngine();
            await RunEditor(editor, draft, "-executeMethod GameForge.Editor.DesktopGeneration.Run", index);
            var completed = Projects.Read(draft.id);
            if (completed.stage != ForgeStage.Complete || !File.Exists(Path.Combine(EngineProject, completed.scenePath)))
                throw new IOException("Unity a quitté avant d’enregistrer la scène. Ton brief est conservé.");
            return completed;
        }

        internal async Task<string> BuildGame(ForgeProject draft, bool android)
        {
            if (draft.stage != ForgeStage.Complete) throw new InvalidOperationException("Crée d’abord le jeu.");
            var editor = FindEditor() ?? throw new InvalidOperationException("Précise l’emplacement de Unity dans Réglages.");
            if (android)
            {
                var folder = Path.Combine(EngineProject, "Builds", "GameForge", draft.variantId);
                var before = Directory.Exists(folder) ? Directory.GetDirectories(folder) : Array.Empty<string>();
                await RunEditor(editor, draft, "-buildTarget Android -executeMethod GameForge.Editor.AndroidApkExport.ExportBatch -gameForgeVariant " + draft.variantId);
                var output = Directory.GetDirectories(folder).Except(before).Select(dir => Path.Combine(dir, "gameforge-android-test.apk")).Single(File.Exists);
                return output;
            }
            await RunEditor(editor, draft, "-buildTarget Win64 -executeMethod GameForge.Editor.DesktopGeneration.Preview");
            var game = Path.Combine(EngineProject, "Builds", "Studio", draft.variantId, "Runner.exe");
            if (!File.Exists(game)) throw new IOException("Le jeu Windows est introuvable.");
            return game;
        }

        private async Task RunEditor(string editor, ForgeProject draft, string job, int index = -1)
        {
            var info = new ProcessStartInfo(editor, "-batchmode -quit -force-d3d11 -projectPath \"" + EngineProject +
                "\" " + job + " -logFile \"" + Log + "\"")
            { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden };
            info.EnvironmentVariables["GAMEFORGE_DRAFT"] = draft.id;
            info.EnvironmentVariables["GAMEFORGE_PLAY"] = index.ToString();
            using var process = Process.Start(info);
            await Task.Run(() => process.WaitForExit());
            if (process.ExitCode != 0) throw new IOException("Unity a interrompu la création. Consulte le journal de l’atelier.");
        }
    }
}
