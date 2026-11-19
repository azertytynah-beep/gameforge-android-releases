using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using GameForge.Runtime;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameForge.Editor
{
    internal sealed class ForgeProjectPanel : VisualElement
    {
        private const int IdeaLimit = ForgeProjectStore.MaxIdeaLength;
        private static readonly string[] StageNames = { "Départ", "Questionnaire", "Apparence", "Gameplay", "Résultat" };
        private readonly ForgeProjectService service;
        private readonly Action<GenerationResult> onCreated;
        private readonly Action close;
        private readonly GameForgeSteps steps;
        private readonly ScrollView page = new ScrollView { name = "forge-project-page" };
        private readonly HelpBox status = new HelpBox(string.Empty, HelpBoxMessageType.Error) { name = "forge-project-status" };
        private ForgeProject project;
        private CancellationTokenSource ideaRequest;
        private ForgeIdeaSuggestions suggestions;
        private ForgeStage? renderedStage;
        private int reachedStage;
        private bool PendingCreation => project?.stage == ForgeStage.Gameplay && !string.IsNullOrEmpty(project.variantId);

        public ForgeProjectPanel(ForgeProjectService service, Action<GenerationResult> onCreated, Action close)
        {
            this.service = service ?? throw new ArgumentNullException(nameof(service));
            this.onCreated = onCreated ?? throw new ArgumentNullException(nameof(onCreated));
            this.close = close ?? throw new ArgumentNullException(nameof(close));
            name = "forge-project-panel";
            AddToClassList("gameforge-project-panel");
            var header = Element("gameforge-project-header");
            header.Add(Button("forge-project-dashboard", "Mes jeux", ShowProjects));
            header.Add(Button("forge-project-close", "Retour à GameForge", () =>
            {
                CancelIdeas();
                close();
            }));
            Add(header);
            steps = new GameForgeSteps(StageNames, index => Perform(() => Navigate(index))) { name = "forge-project-steps" };
            steps.AddToClassList("gameforge-project-steps");
            Add(steps);
            Add(status);
            status.style.display = DisplayStyle.None;
            page.AddToClassList("gameforge-project-page");
            Add(page);
            RegisterCallback<DetachFromPanelEvent>(change =>
            {
                if (change.target == this)
                    CancelIdeas();
            });
        }

        public void ShowProjects()
        {
            CancelIdeas();
            suggestions = null;
            project = null;
            renderedStage = null;
            reachedStage = 0;
            ClearStatus();
            page.Clear();
            page.scrollOffset = Vector2.zero;
            UpdateSteps();
            page.Add(Label("Mes jeux", "gameforge-heading"));
            page.Add(Button("forge-project-start", "Commencer à créer un jeu", StartProject, primary: true));
            foreach (var draft in service.List())
            {
                var card = Element("gameforge-project-tile");
                card.Add(Label(draft.DisplayTitle, "gameforge-preview-title"));
                var pending = draft.stage == ForgeStage.Gameplay && !string.IsNullOrEmpty(draft.variantId);
                card.Add(Label(pending ? "Création en attente" : StageNames[(int)draft.stage], "gameforge-caption"));
                card.Add(Button("forge-project-resume-" + draft.id,
                    draft.stage == ForgeStage.Complete ? "Voir le résultat" : "Reprendre", () => Open(service.Read(draft.id))));
                page.Add(card);
            }
        }

        public void StartProject() => Perform(() => Open(service.Create()));

        private void Open(ForgeProject draft)
        {
            CancelIdeas();
            suggestions = null;
            project = draft;
            renderedStage = null;
            reachedStage = (int)draft.stage;
            if (draft.looks?.Length > 0)
                reachedStage = Math.Max(reachedStage, (int)ForgeStage.Appearance);
            if (draft.playStyles?.Length > 0)
                reachedStage = Math.Max(reachedStage, (int)ForgeStage.Gameplay);
            ShowStage();
        }

        private void Navigate(int index)
        {
            if (!CanNavigate(index) || index == (int)project.stage)
            {
                UpdateSteps();
                return;
            }

            if (project.stage == ForgeStage.Idea)
            {
                StartQuestions();
                return;
            }

            var previous = project.stage;
            try { Save(() => project.stage = (ForgeStage)index); }
            catch
            {
                project.stage = previous;
                UpdateSteps();
                throw;
            }
            ShowStage();
        }

        private bool CanNavigate(int index)
        {
            if (project == null)
                return false;
            if (PendingCreation || project.stage == ForgeStage.Complete)
                return index == (int)project.stage;
            if (index > reachedStage || index >= (int)ForgeStage.Complete ||
                (project.stage == ForgeStage.Idea && index > (int)ForgeStage.Questions))
                return false;
            return (ForgeStage)index switch
            {
                ForgeStage.Appearance => project.looks?.Length > 0,
                ForgeStage.Gameplay => project.chosenLook >= 0 && project.playStyles?.Length > 0,
                _ => true
            };
        }

        private void StartQuestions()
        {
            try { service.StartQuestions(project); }
            catch
            {
                project.stage = ForgeStage.Idea;
                UpdateSteps();
                throw;
            }
            ShowStage();
        }

        private void ShowStage()
        {
            if (project.stage != ForgeStage.Idea)
                CancelIdeas();
            reachedStage = Math.Max(reachedStage, (int)project.stage);
            page.Clear();
            if (renderedStage != project.stage)
                page.scrollOffset = Vector2.zero;
            renderedStage = project.stage;
            UpdateSteps();
            switch (project.stage)
            {
                case ForgeStage.Idea: ShowIdea(); break;
                case ForgeStage.Questions: ShowQuestions(); break;
                case ForgeStage.Appearance: ShowLooks(); break;
                case ForgeStage.Gameplay: ShowGameplay(); break;
                case ForgeStage.Complete: ShowResult(); break;
            }
        }

        private void UpdateSteps()
        {
            int active = project == null ? 0 : (int)project.stage;
            steps.SetActive(active);
            for (int index = 0; index < StageNames.Length; index++)
            {
                var step = steps.Q<Button>($"gameforge-step-{index}");
                step.SetEnabled(CanNavigate(index));
            }
        }

        private void ShowIdea()
        {
            page.Add(Label("Choisis ton point de départ", "gameforge-heading"));
            var title = new TextField("Nom du jeu (facultatif)")
            {
                name = "forge-project-title",
                maxLength = RunnerConfigValidator.MaxTitleLength
            };
            title.textEdition.placeholder = "Sans titre";
            Field(page, title, project.title ?? string.Empty, value => project.title = value);

            var choices = Element("gameforge-project-actions");
            choices.Add(StartChoice("describe", "Décrire mon jeu", ForgeStart.Describe));
            choices.Add(StartChoice("explore", "Trouver une idée", ForgeStart.Explore));
            choices.Add(StartChoice("choose", "Choisir un modèle", ForgeStart.Choose));
            page.Add(choices);

            if (project.start == ForgeStart.Choose)
            {
                var card = Element("gameforge-project-tile");
                card.Add(Label("Endless Runner 3D", "gameforge-preview-title"));
                card.Add(Label("Course à trois voies avec obstacles et commandes tactiles.", "gameforge-caption"));
                page.Add(card);
            }
            else
            {
                var idea = new TextField("Ton idée") { name = "forge-project-idea", multiline = true, maxLength = IdeaLimit };
                idea.AddToClassList("gameforge-project-idea");
                idea.textEdition.placeholder = "Par exemple, une course nocturne qui commence doucement.";
                Field(page, idea, project.idea ?? string.Empty, value =>
                {
                    project.idea = value;
                    DiscardProposals();
                });
                if (project.start == ForgeStart.Explore)
                    ShowIdeaOptions();
            }

            page.Add(Button("forge-project-questionnaire", "Continuer vers le questionnaire", StartQuestions, primary: true));
        }

        private Button StartChoice(string id, string text, ForgeStart choice)
        {
            var button = Button("forge-project-start-" + id, text, () =>
            {
                if (project.start == choice)
                    return;
                CancelIdeas();
                suggestions = null;
                Save(() =>
                {
                    project.start = choice;
                    DiscardProposals();
                });
                ShowStage();
            });
            button.EnableInClassList("gameforge-project-choice--selected", project.start == choice);
            return button;
        }

        private void ShowIdeaOptions()
        {
            var model = new Foldout { text = "Configurer l’IA locale", value = false };
            var modelName = new TextField("Modèle Ollama installé") { name = "forge-project-model", isDelayed = true, maxLength = 128 };
            modelName.SetValueWithoutNotify(service.Model ?? string.Empty);
            modelName.RegisterValueChangedCallback(change => Perform(() => service.Model = change.newValue.Trim()));
            model.Add(modelName);
            page.Add(model);
            if (ideaRequest == null)
                page.Add(Button("forge-project-suggest", "Proposer des idées", RequestIdeas));
            else
                page.Add(Button("forge-project-cancel-suggestion", "Annuler la recherche d’idées", () =>
                {
                    CancelIdeas();
                    ShowStage();
                }));

            if (suggestions == null)
                return;
            page.Add(Label(suggestions.FromAi ? "Idées de l’IA locale" : "Idées de départ pour le Runner 3D", "gameforge-preview-title"));
            foreach (var idea in suggestions.Ideas)
                page.Add(Button("forge-project-use-idea", idea, () =>
                {
                    Save(() =>
                    {
                        project.idea = idea.Length <= IdeaLimit ? idea : idea.Substring(0, IdeaLimit);
                        DiscardProposals();
                    });
                    ShowStage();
                }));
        }

        private async void RequestIdeas()
        {
            var source = new CancellationTokenSource();
            ideaRequest = source;
            string description = project.idea;
            suggestions = null;
            ShowStage();
            try
            {
                var result = await service.SuggestIdeasAsync(description, source.Token);
                if (ideaRequest == source && project.idea == description)
                    suggestions = result;
            }
            catch (OperationCanceledException) { }
            catch (Exception exception)
            {
                if (ideaRequest == source)
                    Report(exception.Message);
            }
            finally
            {
                if (ideaRequest == source)
                {
                    ideaRequest = null;
                    ShowStage();
                }
                source.Dispose();
            }
        }

        private void CancelIdeas()
        {
            var request = ideaRequest;
            ideaRequest = null;
            request?.Cancel();
        }

        private void ShowQuestions()
        {
            var config = project.answers;
            page.Add(Label("Personnalise ton Runner", "gameforge-heading"));
            var theme = Question("Thème", ForgeLocks.Theme);
            var themes = new List<string> { "theme-default", "theme-night" };
            Field(theme, new DropdownField("Décor", themes, 0, ThemeName, ThemeName) { name = "forge-project-theme" },
                config.themeId, value => ChangeAnswer(() => config.themeId = value));

            var pace = Question("Rythme", ForgeLocks.Pace);
            var initial = new Slider("Départ (m/s)", 4, 12) { name = "forge-project-speed", showInputField = true };
            var maximum = new Slider("Maximum (m/s)", 4, 18) { name = "forge-project-speed-max", showInputField = true };
            Field(pace, initial, config.initialSpeed, value => ChangeAnswer(() =>
            {
                config.initialSpeed = value;
                config.maxSpeed = Mathf.Max(config.maxSpeed, value);
                maximum.SetValueWithoutNotify(config.maxSpeed);
            }));
            Field(pace, maximum, config.maxSpeed, value => ChangeAnswer(() =>
            {
                config.maxSpeed = value;
                config.initialSpeed = Mathf.Min(config.initialSpeed, value);
                initial.SetValueWithoutNotify(config.initialSpeed);
            }));
            Field(pace, new Slider("Accélération", 0f, 0.5f) { showInputField = true }, config.acceleration,
                value => ChangeAnswer(() => config.acceleration = value));

            var density = Question("Densité", ForgeLocks.Density);
            Field(density, new SliderInt("Obstacles par segment", 0, 3) { name = "forge-project-density", showInputField = true },
                config.obstacleDensity, value => ChangeAnswer(() => config.obstacleDensity = value));

            var obstacle = Question("Obstacles", ForgeLocks.Obstacle);
            var obstacleOptions = new List<string> { "Blocs", "Barrières", "Blocs et barrières" };
            int selected = config.obstacleIds.Contains("obstacle-barrier") ? (config.obstacleIds.Length > 1 ? 2 : 1) : 0;
            Field(obstacle, new DropdownField("Type", obstacleOptions, selected) { name = "forge-project-obstacle" }, obstacleOptions[selected],
                value => ChangeAnswer(() => config.obstacleIds = value switch
                {
                    "Barrières" => new[] { "obstacle-barrier" },
                    "Blocs et barrières" => new[] { "obstacle-block", "obstacle-barrier" },
                    _ => new[] { "obstacle-block" }
                }));

            ShowObjective(config);
            page.Add(Button("forge-project-propose-looks", "Voir les quatre maquettes", () =>
            {
                service.ProposeLooks(project);
                ShowStage();
            }, primary: true));
        }

        private void ShowObjective(RunnerConfigV1 config)
        {
            var objective = Question("Objectif", ForgeLocks.Objective);
            var labels = new List<string> { "Sans fin", "Distance à atteindre", "Sprint chronométré", "Tenir le chrono" };
            var kind = new DropdownField("Type de partie", labels, (int)config.objective) { name = "forge-project-objective" };
            var distance = new IntegerField("Distance cible (m)") { name = "forge-project-distance", isDelayed = true };
            var duration = new IntegerField("Durée (s)") { name = "forge-project-duration", isDelayed = true };
            void RefreshGoalFields()
            {
                distance.style.display = config.objective is RunnerObjective.Distance or RunnerObjective.Sprint ? DisplayStyle.Flex : DisplayStyle.None;
                duration.style.display = config.objective is RunnerObjective.Sprint or RunnerObjective.Endurance ? DisplayStyle.Flex : DisplayStyle.None;
            }

            Field(objective, kind, labels[(int)config.objective], value => ChangeAnswer(() =>
            {
                config.objective = (RunnerObjective)labels.IndexOf(value);
                if (config.goalDistance == 0 && config.objective is RunnerObjective.Distance or RunnerObjective.Sprint)
                    config.goalDistance = 150;
                if (config.timeLimitSeconds == 0 && config.objective is RunnerObjective.Sprint or RunnerObjective.Endurance)
                    config.timeLimitSeconds = 30;
                distance.SetValueWithoutNotify(config.goalDistance);
                duration.SetValueWithoutNotify(config.timeLimitSeconds);
                RefreshGoalFields();
            }));
            Field(objective, distance, config.goalDistance, value => ChangeAnswer(() =>
            {
                config.goalDistance = Mathf.Clamp(value, 1, RunnerConfigValidator.MaxGoalDistance);
                distance.SetValueWithoutNotify(config.goalDistance);
            }));
            Field(objective, duration, config.timeLimitSeconds, value => ChangeAnswer(() =>
            {
                config.timeLimitSeconds = Mathf.Clamp(value, 1, RunnerConfigValidator.MaxTimeLimitSeconds);
                duration.SetValueWithoutNotify(config.timeLimitSeconds);
            }));
            RefreshGoalFields();
        }

        private VisualElement Question(string label, ForgeLocks flag)
        {
            var group = Element("gameforge-project-tile");
            group.Add(Label(label, "gameforge-preview-title"));
            Field(group, new Toggle("Verrouiller cette réponse") { name = "forge-project-lock-" + flag.ToString().ToLowerInvariant() },
                (project.locks & flag) != 0, enabled => ChangeAnswer(() =>
                    project.locks = enabled ? project.locks | flag : project.locks & ~flag));
            page.Add(group);
            return group;
        }

        private void ChangeAnswer(Action change)
        {
            change();
            DiscardProposals();
        }

        private void DiscardProposals()
        {
            project.looks = null;
            project.playStyles = null;
            project.chosenLook = project.chosenPlay = -1;
            reachedStage = Math.Min(reachedStage, (int)ForgeStage.Questions);
            UpdateSteps();
        }

        private void ShowLooks()
        {
            page.Add(Label("Choisis l’apparence du jeu", "gameforge-heading"));
            var tiles = Element("gameforge-project-grid");
            for (int index = 0; index < project.looks.Length; index++)
            {
                int selected = index;
                var proposal = project.looks[index];
                var card = ProposalTile(tiles, proposal);
                card.Add(Thumbnail(proposal.config));
                card.Add(Button("forge-project-look-" + index, "Retenir cette apparence", () =>
                {
                    service.ChooseLook(project, selected);
                    ShowStage();
                }, primary: true));
            }
            page.Add(tiles);
            page.Add(Button("forge-project-regenerate-looks", "Proposer quatre autres maquettes", () =>
            {
                service.ProposeLooks(project, regenerate: true);
                reachedStage = (int)ForgeStage.Appearance;
                ShowStage();
            }));
        }

        private void ShowGameplay()
        {
            page.Add(Label("Choisis le gameplay", "gameforge-heading"));
            page.Add(Label("Apparence retenue · " + project.looks[project.chosenLook].label, "gameforge-caption"));
            if (PendingCreation)
                page.Add(new HelpBox("Une création est en attente. Reprends le gameplay déjà confirmé pour la terminer.", HelpBoxMessageType.Info));
            var tiles = Element("gameforge-project-grid");
            for (int index = 0; index < project.playStyles.Length; index++)
            {
                int selected = index;
                var proposal = project.playStyles[index];
                var card = ProposalTile(tiles, proposal);
                string pace = proposal.config.initialSpeed.ToString("0.#", CultureInfo.CurrentCulture) + " à " +
                    proposal.config.maxSpeed.ToString("0.#", CultureInfo.CurrentCulture) + " m/s";
                card.Add(Label(pace + " · " + proposal.config.obstacleDensity + " obstacle(s) par segment", "gameforge-caption"));
                card.EnableInClassList("gameforge-project-choice--selected", project.chosenPlay == selected);
                var choose = Button("forge-project-gameplay-" + index, project.chosenPlay == selected ? "Gameplay retenu" : "Retenir ce gameplay", () =>
                {
                    Save(() => project.chosenPlay = selected);
                    ShowStage();
                });
                choose.SetEnabled(!PendingCreation);
                card.Add(choose);
            }
            page.Add(tiles);

            var actions = Element("gameforge-project-actions");
            var regenerate = Button("forge-project-regenerate-gameplay", "Proposer quatre autres gameplays", () =>
            {
                service.RegenerateGameplay(project);
                ShowStage();
            });
            regenerate.SetEnabled(!PendingCreation);
            actions.Add(regenerate);
            var generate = Button("forge-project-generate", PendingCreation ? "Reprendre la création" : "Créer le jeu", Generate, primary: true);
            generate.SetEnabled(project.chosenPlay >= 0 && project.chosenPlay < project.playStyles.Length);
            actions.Add(generate);
            page.Add(actions);
        }

        private void Generate()
        {
            GenerationResult result;
            try { result = service.Generate(project, project.chosenPlay); }
            finally { ShowStage(); }
            if (result?.Succeeded != true)
            {
                Report(result?.Diagnostics?.Count > 0
                    ? string.Join("\n", result.Diagnostics.Select(item => item.Message))
                    : "La création du jeu a échoué.");
                return;
            }

            onCreated(result);
        }

        private void ShowResult()
        {
            page.Add(Label(project.DisplayTitle, "gameforge-heading"));
            page.Add(Label("Variante créée", "gameforge-caption"));
            page.Add(Button("forge-project-open-variant", "Ouvrir dans Variantes", () =>
                onCreated(new GenerationResult(project.variantId, project.scenePath, Array.Empty<RunnerDiagnostic>())), primary: true));
        }

        private static VisualElement ProposalTile(VisualElement grid, ForgeProposal proposal)
        {
            var column = Element("gameforge-project-tile-wrap");
            var card = Element("gameforge-project-tile");
            card.Add(Label(proposal.label, "gameforge-preview-title"));
            card.Add(Label(proposal.summary, "gameforge-caption"));
            column.Add(card);
            grid.Add(column);
            return card;
        }

        private static VisualElement Thumbnail(RunnerConfigV1 config)
        {
            var image = Element("gameforge-project-thumbnail");
            image.pickingMode = PickingMode.Ignore;
            image.generateVisualContent += context =>
            {
                float w = image.contentRect.width;
                float h = image.contentRect.height;
                if (!(w > 0 && h > 0))
                    return;
                var paint = context.painter2D;
                var track = ColorOf(config.trackColor, "#526b57");
                Polygon(paint, ColorOf(config.skyColor, "#263b62"), new Vector2(0, 0), new Vector2(w, 0), new Vector2(w, h), new Vector2(0, h));
                Polygon(paint, track, new Vector2(w * 0.38f, h * 0.36f), new Vector2(w * 0.62f, h * 0.36f), new Vector2(w * 0.92f, h), new Vector2(w * 0.08f, h));
                var mark = Color.Lerp(track, Color.white, 0.76f);
                Polygon(paint, mark, new Vector2(w * 0.456f, h * 0.36f), new Vector2(w * 0.462f, h * 0.36f), new Vector2(w * 0.367f, h), new Vector2(w * 0.357f, h));
                Polygon(paint, mark, new Vector2(w * 0.538f, h * 0.36f), new Vector2(w * 0.544f, h * 0.36f), new Vector2(w * 0.643f, h), new Vector2(w * 0.633f, h));
                Rectangle(paint, ColorOf(config.obstacleColor, "#f3a34d"), w * 0.59f, h * 0.52f, w * 0.09f, h * 0.18f);
                Rectangle(paint, ColorOf(config.playerColor, "#58d6ed"), w * 0.46f, h * 0.66f, w * 0.08f, h * 0.23f);
            };
            return image;
        }

        private static void Rectangle(Painter2D painter, Color color, float x, float y, float width, float height) =>
            Polygon(painter, color, new Vector2(x, y), new Vector2(x + width, y), new Vector2(x + width, y + height), new Vector2(x, y + height));

        private static void Polygon(Painter2D painter, Color color, params Vector2[] points)
        {
            painter.fillColor = color;
            painter.BeginPath();
            painter.MoveTo(points[0]);
            for (int index = 1; index < points.Length; index++)
                painter.LineTo(points[index]);
            painter.ClosePath();
            painter.Fill();
        }

        private static Color ColorOf(string value, string fallback) => ColorUtility.TryParseHtmlString(value, out var color)
            ? color : ColorUtility.TryParseHtmlString(fallback, out var defaultColor) ? defaultColor : Color.gray;

        private void Field<T>(VisualElement parent, BaseField<T> field, T value, Action<T> update)
        {
            field.SetValueWithoutNotify(value);
            field.RegisterValueChangedCallback(change => Perform(() => Save(() => update(change.newValue))));
            parent.Add(field);
        }

        private void Save(Action action)
        {
            action();
            service.Save(project);
        }

        private Button Button(string id, string text, Action action, bool primary = false)
        {
            var button = new Button(() => Perform(action)) { name = id, text = text };
            button.AddToClassList(primary ? "gameforge-primary" : "gameforge-quiet");
            return button;
        }

        private void Perform(Action action)
        {
            ClearStatus();
            try { action(); }
            catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException
                or InvalidOperationException or ArgumentException)
            {
                Report(exception.Message);
            }
        }

        private void ClearStatus() => status.style.display = DisplayStyle.None;

        private void Report(string text)
        {
            status.text = text;
            status.style.display = DisplayStyle.Flex;
        }

        private static string ThemeName(string id) => id == "theme-night" ? "Nocturne" : "Prairie";

        private static VisualElement Element(string className)
        {
            var element = new VisualElement();
            element.AddToClassList(className);
            return element;
        }

        private static Label Label(string text, string className)
        {
            var label = new Label(text);
            label.AddToClassList(className);
            return label;
        }
    }
}
