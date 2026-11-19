using System;
using System.Collections.Generic;
using System.Linq;
using GameForge.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameForge.Editor
{
    public sealed class GameForgeWindow : EditorWindow
    {
        private enum Step { Describe, Adjust, Variants, Export }

        internal const string AppearanceKey = "GameForge.WorkspaceAppearance";
        private const string WindowAssets = "Assets/GameForge/Editor/Window/";
        private static readonly string[] Steps = { "Décrire", "Ajuster", "Variantes", "Exporter" };
        private static readonly string[] Headings = { "Décris ton jeu", "Ajuste les réglages", "Tes variantes", "Exporter sur Android" };
        private static readonly string[] Appearances = { "Unity", "Clair", "Sombre" };

        [SerializeField] private Step activeStep;

        private RunnerConfigurationForm form;
        private VariantPanel variants;
        private bool customBinding;
        private VisualElement workspace;
        private GameForgeSteps navigation;
        private VisualElement body;
        private ForgeProjectPanel projects;
        private VisualElement[] pages;
        private Label heading;
        private TextField gameName;
        private Label exportVariant;
        private Button exportButton;
        private Button revealExport;
        private HelpBox exportStatus;
        private string selectedExportId;
        private string exportedApk;

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
                ShowStep(Step.Adjust);
            });
            variants.AddToClassList("gameforge-card");
            form.AddToClassList("gameforge-card");
            var generate = form.Q<Button>("generate");
            generate.text = "Générer la variante";
            generate.AddToClassList("gameforge-primary");
            form.VariantGenerated -= OnGenerated;
            form.VariantGenerated += OnGenerated;

            workspace = Element("gameforge-workspace");
            workspace.name = "gameforge-workspace";
            var sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(WindowAssets + "GameForgeWindow.uss");
            if (sheet != null)
                workspace.styleSheets.Add(sheet);
            workspace.RegisterCallback<GeometryChangedEvent>(change =>
                workspace.EnableInClassList("gameforge-workspace--compact", change.newRect.width < 710f));

            workspace.Add(CreateHeader());
            navigation = new GameForgeSteps(Steps, index => ShowStep((Step)index));
            workspace.Add(navigation);
            body = Element("gameforge-body");
            projects = catalog == null ? null : new ForgeProjectPanel(
                new ForgeProjectService(new ForgeProjectStore(), catalog),
                result =>
                {
                    ShowProjects(false);
                    OnGenerated(result);
                }, () => ShowProjects(false));
            var content = Element("gameforge-content");
            heading = Text("", "gameforge-heading");
            content.Add(Text("Endless Runner 3D", "gameforge-eyebrow"));
            content.Add(heading);
            pages = new[] { CreateDescription(), form, variants, CreateExport() };
            variants.SelectionChanged += RefreshExportSelection;
            foreach (var page in pages)
                content.Add(page);
            body.Add(content);
            body.Add(CreatePreview());
            workspace.Add(body);
            if (projects != null)
                workspace.Add(projects);
            ShowProjects(false);
            rootVisualElement.Clear();
            rootVisualElement.Add(workspace);
            ShowStep(activeStep);
            UpdateAvailability();
        }

        public void Bind(IRunnerCatalog catalog, IReadOnlyList<string> themeIds,
            IReadOnlyList<string> obstacleIds, IGameGenerator generator)
        {
            form ??= new RunnerConfigurationForm();
            customBinding = true;
            form.Bind(catalog, themeIds, obstacleIds, generator);
        }

        private VisualElement CreateHeader()
        {
            var header = Element("gameforge-header");
            var brand = Element("gameforge-brand");
            brand.Add(Text("GF", "gameforge-logo"));
            brand.Add(new Label("GameForge AI"));

            var selected = Array.IndexOf(Appearances, EditorPrefs.GetString(AppearanceKey, Appearances[0]));
            var appearance = new DropdownField("Apparence", Appearances.ToList(), Mathf.Max(selected, 0))
            {
                name = "gameforge-appearance"
            };
            appearance.AddToClassList("gameforge-appearance");
            appearance.RegisterValueChangedCallback(change =>
            {
                EditorPrefs.SetString(AppearanceKey, change.newValue);
                ApplyAppearance(change.newValue);
            });
            ApplyAppearance(appearance.value);

            header.Add(brand);
            header.Add(appearance);
            return header;
        }

        private void ApplyAppearance(string appearance)
        {
            bool dark = appearance == "Sombre" || (appearance == "Unity" && EditorGUIUtility.isProSkin);
            workspace.EnableInClassList("gameforge-workspace--dark", dark);
            workspace.EnableInClassList("gameforge-workspace--light", !dark);
        }

        private VisualElement CreateDescription()
        {
            var card = Element("gameforge-card");
            card.AddToClassList("gameforge-card--actions");
            if (projects != null)
            {
                card.Add(StyledButton("gameforge-start-project", "Commencer à créer un jeu", "gameforge-primary", () =>
                {
                    ShowProjects(true);
                    projects.StartProject();
                }));
                card.Add(StyledButton("gameforge-projects", "Mes jeux", "gameforge-quiet", () =>
                {
                    ShowProjects(true);
                    projects.ShowProjects();
                }));
            }
            gameName = new TextField("Nom du jeu") { name = "gameforge-game-name", maxLength = RunnerConfigValidator.MaxTitleLength };
            gameName.RegisterValueChangedCallback(change => form.GameTitle = change.newValue);
            card.Add(gameName);
            card.Add(StyledButton("gameforge-next", "Ajuster les réglages", "gameforge-quiet", () => ShowStep(Step.Adjust)));
            return card;
        }

        private void ShowProjects(bool show)
        {
            if (projects == null)
                return;
            navigation.style.display = show ? DisplayStyle.None : DisplayStyle.Flex;
            body.style.display = show ? DisplayStyle.None : DisplayStyle.Flex;
            projects.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private static VisualElement CreatePreview()
        {
            var preview = Element("gameforge-preview");
            preview.Add(Text("Aperçu du modèle", "gameforge-preview-title"));
            var stage = Element("gameforge-preview-stage");
            var image = new Image
            {
                image = AssetDatabase.LoadAssetAtPath<Texture2D>(WindowAssets + "RunnerPreview.png"),
                scaleMode = ScaleMode.ScaleToFit
            };
            image.AddToClassList("gameforge-preview-image");
            stage.Add(image);
            preview.Add(stage);
            preview.Add(Text("Android · Portrait · 3 voies", "gameforge-caption"));
            return preview;
        }

        private VisualElement CreateExport()
        {
            var card = Element("gameforge-card");
            card.AddToClassList("gameforge-card--actions");
            card.Add(Text("APK de test", "gameforge-preview-title"));
            exportVariant = Text("", "gameforge-caption");
            exportVariant.name = "gameforge-export-variant";
            card.Add(exportVariant);
            exportButton = StyledButton("gameforge-export-apk", "Exporter l’APK", "gameforge-primary", ExportApk);
            card.Add(exportButton);
            revealExport = StyledButton("gameforge-reveal-apk", "Afficher le fichier", "gameforge-quiet",
                () => EditorUtility.RevealInFinder(exportedApk));
            card.Add(revealExport);
            exportStatus = new HelpBox("", HelpBoxMessageType.Info);
            card.Add(exportStatus);
            selectedExportId = null;
            exportedApk = null;
            ShowExportStatus();
            return card;
        }

        private void RefreshExportSelection()
        {
            var variant = variants.Selected;
            if (selectedExportId != variant?.Id)
            {
                selectedExportId = variant?.Id;
                exportedApk = null;
                ShowExportStatus();
            }
            exportVariant.text = variant?.Manifest.title ?? "Sélectionne une variante dans l’onglet Variantes.";
            exportButton.SetEnabled(variant != null);
        }

        private void ExportApk()
        {
            var variant = variants.Selected;
            if (variant == null)
                return;

            exportedApk = null;
            try
            {
                var report = AndroidApkExport.Export(variant.Id);
                exportedApk = report.ApkPath;
                ShowExportStatus("APK exporté pour " + variant.Manifest.title + ".");
            }
            catch (OperationCanceledException)
            {
                ShowExportStatus("Export annulé.");
            }
            catch (Exception exception)
            {
                ShowExportStatus(exception.Message, HelpBoxMessageType.Error);
            }
        }

        private void ShowExportStatus(string message = "", HelpBoxMessageType type = HelpBoxMessageType.Info)
        {
            exportStatus.text = message;
            exportStatus.messageType = type;
            exportStatus.style.display = message.Length == 0 ? DisplayStyle.None : DisplayStyle.Flex;
            revealExport.style.display = exportedApk == null ? DisplayStyle.None : DisplayStyle.Flex;
        }

        private void OnEnable() => EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        private void OnDisable() => EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        private void OnPlayModeStateChanged(PlayModeStateChange change) => UpdateAvailability();

        private void UpdateAvailability()
        {
            if (pages == null)
                return;
            bool available = !EditorApplication.isPlayingOrWillChangePlaymode;
            foreach (var page in pages)
                page.SetEnabled(available);
            projects?.SetEnabled(available);
        }

        private void OnGenerated(GenerationResult result)
        {
            variants.Refresh(result.VariantId);
            ShowStep(Step.Variants);
        }

        private void ShowStep(Step step)
        {
            activeStep = step;
            navigation.SetActive((int)step);
            heading.text = Headings[(int)step];
            for (int i = 0; i < pages.Length; i++)
                pages[i].style.display = i == (int)step ? DisplayStyle.Flex : DisplayStyle.None;
            if (step == Step.Describe)
                gameName.SetValueWithoutNotify(form.GameTitle);
            if (step == Step.Export)
                RefreshExportSelection();
        }

        private static VisualElement Element(string style)
        {
            var element = new VisualElement();
            element.AddToClassList(style);
            return element;
        }

        private static Label Text(string value, string style)
        {
            var label = new Label(value);
            label.AddToClassList(style);
            return label;
        }

        private static Button StyledButton(string name, string label, string style, Action click)
        {
            var button = new Button(click) { name = name, text = label };
            button.AddToClassList(style);
            return button;
        }
    }
}
