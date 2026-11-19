using System;
using System.IO;
using System.Linq;
using System.Threading;
using GameForge.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace GameForge.Editor
{
    public sealed class RunnerGenerator : IGameGenerator
    {
        public const string SourceScenePath = "Assets/GameForge/Content/Templates/Runner/RunnerBase.unity";
        public const string CatalogPath = "Assets/GameForge/Content/Catalog/RunnerCatalog.asset";

        private readonly IGenerationStore store;
        private readonly RunnerCatalog catalog;
        private readonly string sourceScenePath;

        public RunnerGenerator(IGenerationStore store, RunnerCatalog catalog, string sourceScenePath = SourceScenePath)
        {
            this.store = store ?? throw new ArgumentNullException(nameof(store));
            this.catalog = catalog;
            this.sourceScenePath = sourceScenePath;
        }

        public GenerationResult Generate(RunnerConfigV1 configuration) => Generate(configuration, CancellationToken.None);

        public GenerationResult Generate(RunnerConfigV1 configuration, CancellationToken cancellation) =>
            Publish(sourceScenePath, configuration, configuration?.title, null, cancellation);

        public GenerationResult Duplicate(StoredVariant source, CancellationToken cancellation = default)
        {
            if (source == null)
                return Failure("Sélectionnez une variante à dupliquer.");

            var scene = SceneManager.GetSceneByPath(source.ScenePath);
            var settings = AssetDatabase.LoadAssetAtPath<RunnerSettings>(source.SettingsPath);
            if (settings == null)
                return Failure("Les réglages de la variante source sont introuvables.");
            if ((scene.IsValid() && scene.isDirty) || EditorUtility.IsDirty(settings))
                return Failure("Enregistrez la scène et les réglages avant la duplication.");

            RunnerConfigV1 config;
            try { config = settings.ToConfig(); }
            catch (NullReferenceException) { return Failure("Les réglages de la variante source sont incomplets."); }
            return Publish(source.ScenePath, config, source.Manifest.title, source.Id, cancellation);
        }

        private GenerationResult Publish(string scenePath, RunnerConfigV1 configuration, string title, string parentId,
            CancellationToken cancellation)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return Failure("Revenez en mode édition avant de générer une variante.");
            if (cancellation.IsCancellationRequested)
                return Failure("Génération annulée.");
            if (!RunnerConfigValidator.TryCreateSettings(configuration, catalog, out var settings, out var diagnostics))
                return new GenerationResult(null, null, diagnostics);

            try
            {
                if (string.IsNullOrEmpty(scenePath) || AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) == null)
                    return Failure("La scène source est introuvable.");

                using var transaction = store.Begin();
                if (!AssetDatabase.CopyAsset(scenePath, transaction.ScenePath))
                    throw new IOException("Impossible de copier la scène source enregistrée.");
                AssetDatabase.CreateAsset(settings, transaction.SettingsPath);
                BindSettings(transaction);
                if (!AssetDatabase.GetDependencies(transaction.ScenePath, false).Contains(transaction.SettingsPath))
                    throw new InvalidDataException("La scène enregistrée ne référence pas ses propres réglages.");
                cancellation.ThrowIfCancellationRequested();

                var variant = transaction.Commit(title, parentId);
                return new GenerationResult(variant.Id, variant.ScenePath, Array.Empty<RunnerDiagnostic>());
            }
            catch (OperationCanceledException)
            {
                return Failure("Génération annulée.");
            }
            catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException
                or ArgumentException or InvalidOperationException or UnityException)
            {
                return Failure(exception.Message);
            }
            finally
            {
                if (settings != null && !EditorUtility.IsPersistent(settings))
                    Object.DestroyImmediate(settings);
            }
        }

        private void BindSettings(GenerationTransaction transaction)
        {
            var previouslyActive = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.OpenScene(transaction.ScenePath, OpenSceneMode.Additive);
            try
            {
                var roots = scene.GetRootGameObjects();
                var bootstraps = roots.SelectMany(root => root.GetComponentsInChildren<RunnerBootstrap>(true)).ToArray();
                if (bootstraps.Length != 1 || roots.SelectMany(root => root.GetComponentsInChildren<Component>(true))
                    .Any(component => component == null))
                    throw new InvalidDataException("La scène doit contenir un RunnerBootstrap et des composants valides.");

                using var bootstrap = new SerializedObject(bootstraps[0]);
                if (bootstrap.FindProperty("catalog").objectReferenceValue != catalog)
                    throw new InvalidDataException("Le catalogue de la scène Runner ne correspond pas au catalogue de génération.");
                foreach (var field in new[] { "motor", "cameraFollow", "session", "stream", "hud" })
                {
                    if (!bootstrap.FindProperty(field).objectReferenceValue)
                        throw new InvalidDataException($"Référence manquante dans RunnerBootstrap. {field}");
                }

                var settings = AssetDatabase.LoadAssetAtPath<RunnerSettings>(transaction.SettingsPath);
                if (settings == null)
                    throw new InvalidDataException("Les réglages générés sont introuvables.");
                bootstrap.FindProperty("settings").objectReferenceValue = settings;
                bootstrap.ApplyModifiedPropertiesWithoutUndo();
                if (!EditorSceneManager.SaveScene(scene))
                    throw new IOException("Impossible d’enregistrer la variante Runner.");
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
                if (previouslyActive.IsValid() && previouslyActive.isLoaded && SceneManager.GetActiveScene() != previouslyActive)
                    SceneManager.SetActiveScene(previouslyActive);
            }
        }

        private static GenerationResult Failure(string message) =>
            new GenerationResult(null, null, new[] { new RunnerDiagnostic("$", message) });
    }
}
