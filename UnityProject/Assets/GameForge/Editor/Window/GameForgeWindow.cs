using System.Collections.Generic;
using System.Linq;
using GameForge.Runtime;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameForge.Editor
{
    public sealed class GameForgeWindow : EditorWindow
    {
        private RunnerConfigurationForm form;
        private VariantPanel variants;
        private bool customBinding;
        [SerializeField] private bool showingVariants;

        [MenuItem("Tools/GameForge")]
        public static void Open() => GetWindow<GameForgeWindow>("GameForge");

        public void CreateGUI()
        {
            minSize = new Vector2(360f, 300f);
            form ??= new RunnerConfigurationForm();
            var store = new GenerationStore();
            var catalog = AssetDatabase.LoadAssetAtPath<RunnerCatalog>(RunnerGenerator.CatalogPath);
            var generator = catalog != null ? new RunnerGenerator(store, catalog) : null;
            if (!customBinding)
                form.Bind(catalog, catalog?.Themes.Select(theme => theme.Id).ToArray(),
                    catalog?.Obstacles.Select(obstacle => obstacle.Id).ToArray(), generator);

            variants = new VariantPanel(store, catalog, generator, configuration =>
            {
                form.Load(configuration);
                ShowVariants(false);
            });
            form.VariantGenerated -= OnGenerated;
            form.VariantGenerated += OnGenerated;

            var tabs = new Toolbar();
            tabs.Add(new ToolbarButton(() => ShowVariants(false)) { text = "Configuration" });
            tabs.Add(new ToolbarButton(() => ShowVariants(true)) { text = "Variantes" });
            rootVisualElement.Clear();
            rootVisualElement.Add(tabs);
            rootVisualElement.Add(form);
            rootVisualElement.Add(variants);
            ShowVariants(showingVariants);
            UpdateAvailability();
        }

        public void Bind(IRunnerCatalog catalog, IReadOnlyList<string> themeIds,
            IReadOnlyList<string> obstacleIds, IGameGenerator generator)
        {
            form ??= new RunnerConfigurationForm();
            customBinding = true;
            form.Bind(catalog, themeIds, obstacleIds, generator);
        }

        private void OnEnable() => EditorApplication.playModeStateChanged += OnPlayModeStateChanged;

        private void OnDisable() => EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;

        private void OnPlayModeStateChanged(PlayModeStateChange change) => UpdateAvailability();

        private void UpdateAvailability()
        {
            bool available = !EditorApplication.isPlayingOrWillChangePlaymode;
            form?.SetEnabled(available);
            variants?.SetEnabled(available);
        }

        private void OnGenerated(GenerationResult result)
        {
            variants.Refresh(result.VariantId);
            ShowVariants(true);
        }

        private void ShowVariants(bool show)
        {
            showingVariants = show;
            variants.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            form.style.display = show ? DisplayStyle.None : DisplayStyle.Flex;
        }
    }
}
