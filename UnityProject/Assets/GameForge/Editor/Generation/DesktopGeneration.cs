using System;
using System.IO;
using GameForge.Runtime;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace GameForge.Editor
{
    public static class DesktopGeneration
    {
        public static void Preview()
        {
            if (!Application.isBatchMode || EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneWindows64)
                throw new InvalidOperationException("L’aperçu requiert la cible Windows 64 bits.");
            var draft = new ForgeProjectStore().Read(Environment.GetEnvironmentVariable("GAMEFORGE_DRAFT"));
            if (draft.stage != ForgeStage.Complete)
                throw new InvalidOperationException("Crée d’abord le jeu.");
            var file = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Builds", "Studio", draft.variantId, "Runner.exe"));
            Directory.CreateDirectory(Path.GetDirectoryName(file));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { draft.scenePath }, target = BuildTarget.StandaloneWindows64, locationPathName = file
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new IOException("La compilation de l’aperçu a échoué.");
        }

        public static void Run()
        {
            if (!Application.isBatchMode)
                throw new InvalidOperationException("La génération depuis le Studio utilise Unity en arrière-plan.");
            var service = new ForgeProjectService(new ForgeProjectStore(),
                AssetDatabase.LoadAssetAtPath<RunnerCatalog>(RunnerGenerator.CatalogPath));
            var draft = service.Read(Environment.GetEnvironmentVariable("GAMEFORGE_DRAFT"));
            if (draft.stage == ForgeStage.Complete)
                return;
            if (!int.TryParse(Environment.GetEnvironmentVariable("GAMEFORGE_PLAY"), out int index) ||
                index < 0 || index >= ForgeProject.ProposalCount)
                throw new InvalidDataException("Choisis un gameplay avant de créer le jeu.");
            var result = service.Generate(draft, index);
            if (!result.Succeeded)
                throw new InvalidOperationException(result.Diagnostics[0].Message);
        }
    }
}
