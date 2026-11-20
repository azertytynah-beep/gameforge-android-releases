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
    internal sealed partial class ForgeProjectPanel : VisualElement
    {
        private const int IdeaLimit = ForgeProjectStore.MaxIdeaLength;
        private static readonly string[] StageNames = { "Départ", "Questionnaire", "Apparence", "Gameplay", "Résultat" };
        private readonly ForgeProjectService service;
        private readonly Action<GenerationResult> onCreated;
        private readonly Action close;
        private readonly GameForgeSteps steps;
        private readonly ScrollView page = new ScrollView { name = "forge-project-page" };
        private readonly HelpBox status = new HelpBox(string.Empty, HelpBoxMessageType.Error) { name = "forge-project-status" };
        private readonly VisualElement footer = Element("gameforge-atelier-footer");
        private readonly VisualElement header = Element("gameforge-project-header");
        private readonly Label projectTitle = Label("Atelier", "gameforge-atelier-title");
        private readonly Button cancelOmni;
        private ForgeProject project;
        private ForgeDictation dictation;
        private CancellationTokenSource ideaRequest;
        private CancellationTokenSource omniRequest;
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
            var atelier = Element("gameforge-atelier");
            var frame = Element("gameforge-atelier-frame");
            var links = Element("gameforge-project-actions");
            links.Add(Button("forge-project-dashboard", "Mes jeux", ShowProjects));
            links.Add(Button("forge-project-close", "Retour à GameForge", () =>
            {
                dictation?.Stop();
                CancelIdeas();
                AbandonOmniRequest();
                close();
            }));
            header.Add(projectTitle);
            header.Add(links);
            atelier.Add(header);
            steps = new GameForgeSteps(StageNames, index => Perform(() => Navigate(index))) { name = "forge-project-steps" };
            steps.AddToClassList("gameforge-project-steps");
            atelier.Add(steps);
            status.style.display = DisplayStyle.None;
            page.AddToClassList("gameforge-project-page");
            frame.Add(page);
            frame.Add(status);
            cancelOmni = new Button(() => omniRequest?.Cancel()) { name = "forge-project-cancel-omni", text = "Annuler la demande" };
            cancelOmni.AddToClassList("gameforge-quiet");
            cancelOmni.style.display = DisplayStyle.None;
            frame.Add(cancelOmni);
            frame.Add(footer);
            atelier.Add(frame);
            Add(atelier);
            RegisterCallback<DetachFromPanelEvent>(change =>
            {
                if (change.target == this)
                {
                    CancelIdeas();
                    AbandonOmniRequest();
                    dictation?.Dispose();
                    dictation = null;
                }
            });
        }

        public void ShowProjects()
        {
            CancelIdeas();
            AbandonOmniRequest();
            dictation?.Stop();
            suggestions = null;
            project = null;
            renderedStage = null;
            reachedStage = 0;
            ClearStatus();
            page.Clear();
            footer.Clear();
            page.scrollOffset = Vector2.zero;
            projectTitle.text = "Atelier";
            UpdateSteps();
            page.Add(Label("Mes jeux", "gameforge-heading"));
            footer.Add(Button("forge-project-start", "Commencer à créer un jeu", StartProject, primary: true));
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
            AbandonOmniRequest();
            dictation?.Stop();
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
            if (project == null || omniRequest != null)
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
            dictation?.Stop();
            if (project.stage != ForgeStage.Idea)
                CancelIdeas();
            reachedStage = Math.Max(reachedStage, (int)project.stage);
            page.Clear();
            footer.Clear();
            projectTitle.text = project.DisplayTitle;
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
            Field(page, title, project.title ?? string.Empty, value =>
            {
                project.title = value;
                projectTitle.text = project.DisplayTitle;
            });

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
                AddDictation(page, idea);
                if (project.start == ForgeStart.Explore)
                    ShowIdeaOptions();
            }

            footer.Add(Button("forge-project-questionnaire", "Continuer vers le questionnaire", StartQuestions, primary: true));
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
            page.Add(Label("Imagine ton jeu", "gameforge-heading"));
            page.Add(Label("Verrouille les réponses qui doivent guider toutes les propositions.", "gameforge-caption"));
            var tabs = Element("gameforge-atelier-chapters");
            for (int i = 0; i < ForgeBrief.Chapters.Length; i++)
            {
                int index = i;
                var tab = Button("forge-project-chapter-" + index, ForgeBrief.Chapters[index].Title, () => OpenChapter(index));
                tab.EnableInClassList("gameforge-project-choice--selected", index == project.briefChapter);
                tabs.Add(tab);
            }
            page.Add(tabs);
            var chapter = ForgeBrief.Chapters[project.briefChapter];
            var chapterCard = Element("gameforge-atelier-chapter");
            foreach (var prompt in chapter.Prompts)
            {
                var id = prompt.Id;
                var block = Element("gameforge-atelier-answer");
                var answer = new TextField(prompt.Required ? prompt.Question + " (requis)" : prompt.Question)
                {
                    name = "forge-project-brief-" + id,
                    multiline = true,
                    maxLength = ForgeBrief.AnswerLimit
                };
                answer.AddToClassList("gameforge-atelier-textarea");
                answer.textEdition.placeholder = prompt.Example;
                Field(block, answer, project.brief.Get(id), value => ChangeAnswer(() => project.brief.Set(id, value)));
                var options = Element("gameforge-project-actions");
                Field(options, new Toggle("Verrouiller cette réponse") { name = "forge-project-brief-lock-" + id },
                    project.brief.IsLocked(id), value => ChangeAnswer(() => project.brief.SetLocked(id, value)));
                AddDictation(options, answer);
                if (prompt.Required)
                {
                    options.Add(Button("forge-project-brief-delegate-" + id, "Laisser GameForge proposer", () =>
                        answer.value = ForgeBrief.DelegateAnswer));
                }
                block.Add(options);
                chapterCard.Add(block);
            }
            page.Add(chapterCard);

            if (project.briefChapter > 0)
                footer.Add(Button("forge-project-prev-chapter", "Chapitre précédent", () => OpenChapter(project.briefChapter - 1)));
            if (project.briefChapter < ForgeBrief.Chapters.Length - 1)
            {
                footer.Add(Button("forge-project-next-chapter", "Chapitre suivant", () => OpenChapter(project.briefChapter + 1), primary: true));
            }
            else
            {
                var advanced = new Foldout { name = "forge-project-advanced", text = "Paramètres avancés", value = false };
                advanced.AddToClassList("gameforge-atelier-settings");
                ShowAdvancedQuestions(advanced, project.answers);
                page.Add(advanced);
                footer.Add(Button("forge-project-propose-looks", "Voir les quatre maquettes", () =>
                {
                    if (FindMissingAnswer())
                        return;
                    service.ProposeLooks(project);
                    ShowStage();
                }, primary: true));
                AddOmniRoute(gameplay: false, regenerate: false);
            }
        }

        private void OpenChapter(int index)
        {
            if (project.briefChapter != index)
            {
                Save(() => { project.briefChapter = index; project.briefQuestion = 0; });
                page.scrollOffset = Vector2.zero;
                ShowStage();
            }
        }

        private bool FindMissingAnswer()
        {
            if (!project.brief.detailed)
                return false;
            int missing = project.brief.MissingChapter;
            if (missing < 0)
                return false;
            OpenChapter(missing);
            Report("Complète les réponses requises de ce chapitre ou choisis « Laisser GameForge proposer ».");
            return true;
        }

        private void ShowAdvancedQuestions(VisualElement parent, RunnerConfigV1 config)
        {
            var theme = Question(parent, "Thème", ForgeLocks.Theme);
            var themes = new List<string> { "theme-default", "theme-night" };
            Field(theme, new DropdownField("Décor", themes, 0, ThemeName, ThemeName) { name = "forge-project-theme" },
                config.themeId, value => ChangeAnswer(() => config.themeId = value));

            var pace = Question(parent, "Rythme", ForgeLocks.Pace);
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

            var density = Question(parent, "Densité", ForgeLocks.Density);
            Field(density, new SliderInt("Obstacles par segment", 0, 3) { name = "forge-project-density", showInputField = true },
                config.obstacleDensity, value => ChangeAnswer(() => config.obstacleDensity = value));

            var obstacle = Question(parent, "Obstacles", ForgeLocks.Obstacle);
            var obstacleOptions = new List<string> { "Blocs", "Barrières", "Blocs et barrières" };
            int selected = config.obstacleIds.Contains("obstacle-barrier") ? (config.obstacleIds.Length > 1 ? 2 : 1) : 0;
            Field(obstacle, new DropdownField("Type", obstacleOptions, selected) { name = "forge-project-obstacle" }, obstacleOptions[selected],
                value => ChangeAnswer(() => config.obstacleIds = value switch
                {
                    "Barrières" => new[] { "obstacle-barrier" },
                    "Blocs et barrières" => new[] { "obstacle-block", "obstacle-barrier" },
                    _ => new[] { "obstacle-block" }
                }));

            ShowObjective(parent, config);
        }

        private void ShowObjective(VisualElement parent, RunnerConfigV1 config)
        {
            var objective = Question(parent, "Objectif", ForgeLocks.Objective);
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

        private VisualElement Question(VisualElement parent, string label, ForgeLocks flag)
        {
            var group = Element("gameforge-project-tile");
            group.Add(Label(label, "gameforge-preview-title"));
            Field(group, new Toggle("Verrouiller cette réponse") { name = "forge-project-lock-" + flag.ToString().ToLowerInvariant() },
                (project.locks & flag) != 0, enabled => ChangeAnswer(() =>
                    project.locks = enabled ? project.locks | flag : project.locks & ~flag));
            parent.Add(group);
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
            footer.Add(Button("forge-project-regenerate-looks", "Proposer quatre autres maquettes", () =>
            {
                service.ProposeLooks(project, regenerate: true);
                reachedStage = (int)ForgeStage.Appearance;
                ShowStage();
            }));
            AddOmniRoute(gameplay: false, regenerate: true);
        }

        private void ShowGameplay()
        {
            page.Add(Label("Choisis le gameplay", "gameforge-heading"));
            var appearance = project.looks[project.chosenLook];
            var preview = Element("gameforge-atelier-kept-preview");
            preview.Add(Label("Apparence retenue · " + appearance.label, "gameforge-preview-title"));
            preview.Add(Thumbnail(appearance.config));
            page.Add(preview);
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

            var regenerate = Button("forge-project-regenerate-gameplay", "Proposer quatre autres gameplays", () =>
            {
                service.RegenerateGameplay(project);
                ShowStage();
            });
            regenerate.SetEnabled(!PendingCreation);
            footer.Add(regenerate);
            var generate = Button("forge-project-generate", PendingCreation ? "Reprendre la création" : "Créer le jeu", Generate, primary: true);
            generate.SetEnabled(project.chosenPlay >= 0 && project.chosenPlay < project.playStyles.Length);
            footer.Add(generate);
            if (!PendingCreation)
                AddOmniRoute(gameplay: true, regenerate: true);
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
            footer.Add(Button("forge-project-open-variant", "Ouvrir dans Variantes", () =>
                onCreated(new GenerationResult(project.variantId, project.scenePath, Array.Empty<RunnerDiagnostic>())), primary: true));
        }

        private static VisualElement ProposalTile(VisualElement grid, ForgeProposal proposal)
        {
            var column = Element("gameforge-project-tile-wrap");
            var card = Element("gameforge-project-tile");
            card.Add(Label(proposal.label, "gameforge-preview-title"));
            card.Add(Label(proposal.summary, "gameforge-caption"));
            foreach (var extra in new[] { proposal.detail, proposal.enemyStyle, proposal.assetNote })
            {
                if (!string.IsNullOrWhiteSpace(extra))
                    card.Add(Label(extra, "gameforge-atelier-proposal-detail"));
            }
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
                float scale = Mathf.Min(w, h * 2.4f);
                if (config.obstacleDensity > 0)
                    DrawEnemyThumbnail(paint, config.enemyDesign, ColorOf(config.obstacleColor, "#f3a34d"), w * .62f, h * .60f, scale * .16f);
                Color player = ColorOf(config.playerColor, "#58d6ed");
                Vector2 center = new Vector2(w * .5f, h * .78f);
                float radius = h * .085f;
                Vector2 Point(float x, float y) => center + new Vector2(x * radius, y * radius);
                Polygon(paint, Color.Lerp(player, Color.black, .24f), Point(-1, -.75f), Point(-.65f, -1.3f), Point(.65f, -1.3f),
                    Point(1, -.75f), Point(1, .78f), Point(.65f, 1.25f), Point(-.65f, 1.25f), Point(-1, .78f));
                Polygon(paint, player, Point(-.8f, -.78f), Point(-.51f, -1.11f), Point(.54f, -1.11f), Point(.75f, -.78f),
                    Point(.69f, .53f), Point(.44f, .88f), Point(-.53f, .88f), Point(-.77f, .53f));
            };
            return image;
        }

        private static void DrawEnemyThumbnail(Painter2D paint, RunnerEnemyDesign design, Color shade, float x, float y, float size)
        {
            Vector2 Point(float dx, float dy) => new Vector2(x + dx * size, y + dy * size);
            void Box(Color color, float x0, float y0, float w, float h) =>
                Rectangle(paint, color, x + x0 * size, y + y0 * size, w * size, h * size);
            Color rim = Color.Lerp(shade, Color.white, .3f);
            Color side = Color.Lerp(shade, Color.black, .27f);
            Polygon(paint, new Color(0, 0, 0, .2f), Point(-.62f, .47f), Point(.28f, .41f), Point(.56f, .62f), Point(-.44f, .68f));
            switch (design)
            {
                case RunnerEnemyDesign.Robot:
                    Box(side, -.54f, -.13f, 1.08f, .33f);
                    Box(shade, -.34f, -.16f, .68f, .61f);
                    Box(rim, -.29f, -.56f, .58f, .32f);
                    Box(side, -.19f, -.46f, .38f, .10f);
                    break;
                case RunnerEnemyDesign.Creature:
                    Polygon(paint, shade, Point(-.48f, -.10f), Point(-.35f, -.49f), Point(-.12f, -.24f),
                        Point(.12f, -.24f), Point(.35f, -.49f), Point(.48f, -.10f), Point(.32f, .37f), Point(-.29f, .45f));
                    Polygon(paint, rim, Point(-.23f, -.22f), Point(.22f, -.22f), Point(.12f, .03f), Point(-.18f, .08f));
                    break;
                case RunnerEnemyDesign.Drone:
                    Box(side, -.57f, -.17f, 1.14f, .20f);
                    Box(rim, -.63f, -.41f, .40f, .15f);
                    Box(rim, .23f, -.41f, .40f, .15f);
                    Polygon(paint, shade, Point(-.39f, -.22f), Point(.29f, -.29f), Point(.38f, .14f),
                        Point(.03f, .39f), Point(-.41f, .21f));
                    break;
                case RunnerEnemyDesign.Golem:
                    Polygon(paint, shade, Point(-.54f, .29f), Point(-.41f, -.27f), Point(.16f, -.54f),
                        Point(.55f, -.20f), Point(.45f, .43f), Point(-.08f, .54f));
                    Polygon(paint, rim, Point(-.41f, -.27f), Point(.16f, -.54f), Point(.55f, -.20f), Point(-.14f, -.06f));
                    break;
                case RunnerEnemyDesign.Plant:
                    Box(side, -.12f, -.18f, .24f, .69f);
                    Polygon(paint, shade, Point(-.65f, .04f), Point(-.28f, -.17f), Point(.02f, .13f), Point(-.23f, .30f));
                    Polygon(paint, rim, Point(.08f, .05f), Point(.21f, -.47f), Point(.59f, -.53f), Point(.43f, -.06f));
                    break;
                default:
                    Box(shade, -.44f, -.38f, .88f, .85f);
                    Polygon(paint, rim, Point(-.44f, -.38f), Point(-.25f, -.56f), Point(.61f, -.56f), Point(.44f, -.38f));
                    break;
            }
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
            var button = new Button(() => Perform(action)) { name = id, text = text, enableRichText = false };
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

        private void Report(string text, HelpBoxMessageType type = HelpBoxMessageType.Error)
        {
            status.text = text;
            status.messageType = type;
            var body = status.Q<Label>();
            if (body != null)
                body.enableRichText = false;
            status.style.display = DisplayStyle.Flex;
        }

        private void AddDictation(VisualElement parent, TextField field)
        {
            dictation ??= new ForgeDictation();
            parent.Add(dictation.ButtonFor(field));
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
            var label = new Label(text) { enableRichText = false };
            label.AddToClassList(className);
            return label;
        }
    }
}
