using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GameForge.Runtime;
using UnityEngine;

namespace GameForge.Editor
{
    internal sealed class ForgeProjectService
    {
        private const int ProposalCount = ForgeProject.ProposalCount;
        private const string ModelKey = "GameForge.LocalIdeasModel";
        private static readonly string[] Palettes =
        {
            "Atlantique", "Encre", "Lilas", "Grenat", "Corail", "Ambre", "Citron", "Mousse", "Jade", "Lagon"
        };
        private static readonly float[] PaletteHues = { .56f, .64f, .75f, .93f, .02f, .09f, .15f, .24f, .36f, .47f };
        private static readonly string[] Objectives = { "Sans fin", "Distance", "Sprint", "Endurance" };
        private readonly ForgeProjectStore projects;
        private readonly RunnerCatalog catalog;

        public ForgeProjectService(ForgeProjectStore projects, RunnerCatalog catalog)
        {
            this.projects = projects;
            this.catalog = catalog;
        }

        public string Model
        {
            get => UnityEditor.EditorPrefs.GetString(ModelKey, "");
            set => UnityEditor.EditorPrefs.SetString(ModelKey, value?.Trim() ?? "");
        }

        public ForgeProject Create() => projects.Create();
        public ForgeProject Read(string id) => projects.Read(id);
        public IReadOnlyList<ForgeProject> List() => projects.List();
        public void Save(ForgeProject draft) => projects.Save(draft);

        public Task<ForgeIdeaSuggestions> SuggestIdeasAsync(string description, CancellationToken cancellation) =>
            ForgeLocalIdeas.SuggestAsync(Model, description, cancellation);

        public void StartQuestions(ForgeProject draft)
        {
            if (draft.stage != ForgeStage.Idea)
                throw new InvalidOperationException("Ouvre le départ du projet.");
            if (draft.start == ForgeStart.Describe && string.IsNullOrWhiteSpace(draft.idea))
                throw new InvalidOperationException("Décris ton idée ou choisis le modèle Runner 3D.");
            draft.stage = ForgeStage.Questions;
            Save(draft);
        }

        public void ProposeLooks(ForgeProject draft, bool regenerate = false)
        {
            RequireStage(draft, regenerate ? ForgeStage.Appearance : ForgeStage.Questions);
            RequireNoPendingVariant(draft);
            var answers = draft.answers.Copy();
            answers.title = draft.DisplayTitle;
            RequireValid(answers);
            draft.lookRound = regenerate ? checked(draft.lookRound + 1) : 0;
            int firstPalette = (int)((uint)Seed(answers.seed, draft.idea) % (uint)Palettes.Length);
            draft.looks = Enumerable.Range(0, ProposalCount).Select(index =>
            {
                int palette = (firstPalette + (draft.lookRound % Palettes.Length) * ProposalCount + index) % Palettes.Length;
                var look = answers.Copy();
                if (!Locked(draft, ForgeLocks.Theme) && catalog.Themes.Count > 1 && index % 2 != 0)
                    look.themeId = catalog.Themes.First(theme => theme.Id != answers.themeId).Id;
                bool night = look.themeId == "theme-night";
                float hue = PaletteHues[palette];
                look.skyColor = Hex(hue, night ? .57f : .35f, night ? .24f : .86f);
                look.trackColor = Hex(hue, .36f, night ? .42f : .47f);
                look.playerColor = Hex(hue + .12f, .45f, 1f);
                look.obstacleColor = Hex(hue + .48f, .68f, 1f);
                return new ForgeProposal
                {
                    label = Palettes[palette],
                    summary = night ? "Ciel nocturne et obstacles lumineux." : "Ciel clair et obstacles contrastés.",
                    config = look
                };
            }).ToArray();
            draft.chosenLook = draft.chosenPlay = -1;
            draft.playRound = 0;
            draft.playStyles = null;
            draft.stage = ForgeStage.Appearance;
            Save(draft);
        }

        public void ChooseLook(ForgeProject draft, int index)
        {
            RequireStage(draft, ForgeStage.Appearance);
            RequireNoPendingVariant(draft);
            ProposalAt(draft.looks, index);
            draft.playRound = 0;
            draft.chosenLook = index;
            ProposeGameplay(draft);
        }

        public void RegenerateGameplay(ForgeProject draft)
        {
            RequireStage(draft, ForgeStage.Gameplay);
            RequireNoPendingVariant(draft);
            draft.playRound = checked(draft.playRound + 1);
            ProposeGameplay(draft);
        }

        private void ProposeGameplay(ForgeProject draft)
        {
            var look = ProposalAt(draft.looks, draft.chosenLook).config;
            RequireValid(look);
            var obstacles = catalog.Obstacles.Select(obstacle => obstacle.Id).ToArray();
            draft.playStyles = Enumerable.Range(0, ProposalCount).Select(index =>
            {
                var play = look.Copy();
                play.seed = unchecked(Seed(play.seed, draft.id) + draft.playRound * ProposalCount + index);
                int variation = (index + draft.playRound % ProposalCount) % ProposalCount;
                if (!Locked(draft, ForgeLocks.Pace))
                {
                    float nudge = new[] { 0f, -1.5f, 1f, 2f }[variation];
                    play.initialSpeed = Mathf.Clamp(play.initialSpeed + nudge, 4f, 12f);
                    play.maxSpeed = Mathf.Clamp(play.maxSpeed + nudge, play.initialSpeed, 18f);
                    if (play.objective == RunnerObjective.Sprint && Locked(draft, ForgeLocks.Objective) &&
                        DistanceInTime(play) < play.goalDistance && DistanceInTime(look) >= look.goalDistance)
                    {
                        play.initialSpeed = look.initialSpeed;
                        play.maxSpeed = look.maxSpeed;
                    }
                }
                if (!Locked(draft, ForgeLocks.Density) && variation != 0)
                    play.obstacleDensity = (Math.Max(1, play.obstacleDensity) - 1 + variation) % 3 + 1;
                if (!Locked(draft, ForgeLocks.Obstacle) && variation != 0 && obstacles.Length > 0)
                    play.obstacleIds = new[] { obstacles[(variation - 1) % obstacles.Length] };
                if (play.obstacleIds.Length == 0 && play.obstacleDensity > 0)
                    play.obstacleDensity = 0;

                if (!Locked(draft, ForgeLocks.Objective))
                {
                    play.objective = (RunnerObjective)(((int)look.objective + index) % Objectives.Length);
                    if (play.objective != look.objective || draft.playRound > 0)
                    {
                        play.goalDistance = play.objective is RunnerObjective.Distance or RunnerObjective.Sprint
                            ? 100 + (draft.playRound % 9) * 15 : 0;
                        play.timeLimitSeconds = play.objective is RunnerObjective.Sprint or RunnerObjective.Endurance
                            ? 30 + (draft.playRound % 7) * 5 : 0;
                    }
                    if (play.objective == RunnerObjective.Sprint)
                        play.goalDistance = Math.Min(play.goalDistance, (int)DistanceInTime(play));
                }
                var goal = play.objective switch
                {
                    RunnerObjective.Distance => $"Atteindre {play.goalDistance} m.",
                    RunnerObjective.Sprint => $"Atteindre {play.goalDistance} m en {play.timeLimitSeconds} s.",
                    RunnerObjective.Endurance => $"Survivre {play.timeLimitSeconds} s.",
                    _ => "Aller le plus loin possible."
                };
                RequireValid(play);
                return new ForgeProposal
                {
                    label = Locked(draft, ForgeLocks.Objective)
                        ? $"{Objectives[(int)play.objective]} · parcours {index + 1}" : Objectives[(int)play.objective],
                    summary = goal,
                    config = play
                };
            }).ToArray();
            draft.stage = ForgeStage.Gameplay;
            draft.chosenPlay = -1;
            Save(draft);
        }

        public GenerationResult Generate(ForgeProject draft, int index)
        {
            RequireStage(draft, ForgeStage.Gameplay);
            if (!string.IsNullOrEmpty(draft.variantId) && index != draft.chosenPlay)
                throw new InvalidOperationException("Reprends le gameplay déjà confirmé pour terminer la création interrompue.");

            var appearance = ProposalAt(draft.looks, draft.chosenLook).config;
            var selected = ProposalAt(draft.playStyles, index).config.Copy();
            selected.title = draft.DisplayTitle;
            RequireValid(selected);
            if (!SameAppearance(appearance, selected) || !KeepsLockedAnswers(draft, selected))
                throw new InvalidDataException("Cette proposition ne respecte plus les choix du projet. Reviens au questionnaire.");

            var store = new GenerationStore();
            GenerationResult result;
            if (!string.IsNullOrEmpty(draft.variantId) && Directory.Exists(Path.Combine(
                    Application.dataPath, "GameForgeGenerated", draft.variantId)))
            {
                var existing = store.ReadVariant(draft.variantId);
                result = new GenerationResult(existing.Id, existing.ScenePath, Array.Empty<RunnerDiagnostic>());
            }
            else
            {
                if (string.IsNullOrEmpty(draft.variantId) || store.ListInterrupted().Contains(draft.variantId))
                    draft.variantId = Guid.NewGuid().ToString("N");
                draft.chosenPlay = index;
                Save(draft);
                var reserved = new GenerationStore(() => Guid.ParseExact(draft.variantId, "N"));
                result = new RunnerGenerator(reserved, catalog).Generate(selected);
            }
            if (result.Succeeded)
            {
                draft.scenePath = result.ScenePath;
                draft.stage = ForgeStage.Complete;
            }
            else
            {
                draft.variantId = null;
                draft.chosenPlay = -1;
            }
            Save(draft);
            return result;
        }

        private static bool SameAppearance(RunnerConfigV1 look, RunnerConfigV1 play) =>
            look.themeId == play.themeId && look.skyColor == play.skyColor && look.trackColor == play.trackColor &&
            look.playerColor == play.playerColor && look.obstacleColor == play.obstacleColor;

        private static bool KeepsLockedAnswers(ForgeProject draft, RunnerConfigV1 play)
        {
            var answers = draft.answers;
            return (!Locked(draft, ForgeLocks.Theme) || answers.themeId == play.themeId) &&
                (!Locked(draft, ForgeLocks.Pace) || (answers.initialSpeed == play.initialSpeed &&
                    answers.maxSpeed == play.maxSpeed && answers.acceleration == play.acceleration)) &&
                (!Locked(draft, ForgeLocks.Density) || answers.obstacleDensity == play.obstacleDensity) &&
                (!Locked(draft, ForgeLocks.Obstacle) || (answers.obstacleIds ?? Array.Empty<string>()).SequenceEqual(play.obstacleIds)) &&
                (!Locked(draft, ForgeLocks.Objective) || (answers.objective == play.objective &&
                    answers.goalDistance == play.goalDistance && answers.timeLimitSeconds == play.timeLimitSeconds));
        }

        private void RequireValid(RunnerConfigV1 config)
        {
            var diagnostics = RunnerConfigValidator.Validate(config, catalog);
            if (diagnostics.Count > 0)
                throw new InvalidOperationException(diagnostics[0].Message);
            if (config.objective == RunnerObjective.Sprint && config.goalDistance > DistanceInTime(config))
                throw new InvalidOperationException("Pour rendre le sprint gagnable, réduis la distance ou augmente le temps ou la vitesse.");
        }

        private static float DistanceInTime(RunnerConfigV1 config)
        {
            if (config.acceleration <= 0f)
                return config.initialSpeed * config.timeLimitSeconds;
            float ramp = Mathf.Min(config.timeLimitSeconds, (config.maxSpeed - config.initialSpeed) / config.acceleration);
            return ramp * (config.initialSpeed + config.acceleration * ramp / 2f) +
                (config.timeLimitSeconds - ramp) * config.maxSpeed;
        }

        private static void RequireStage(ForgeProject draft, ForgeStage stage)
        {
            if (draft.stage != stage)
                throw new InvalidOperationException("Ouvre l’étape correspondante dans le projet.");
        }

        private static void RequireNoPendingVariant(ForgeProject draft)
        {
            if (!string.IsNullOrEmpty(draft.variantId))
                throw new InvalidOperationException("Termine d’abord la création interrompue depuis l’étape Gameplay.");
        }

        private static ForgeProposal ProposalAt(ForgeProposal[] proposals, int index)
        {
            if (proposals?.Length != ProposalCount || index < 0 || index >= ProposalCount)
                throw new InvalidOperationException("Choisis une des quatre propositions.");
            return proposals[index];
        }

        private static bool Locked(ForgeProject draft, ForgeLocks choice) => (draft.locks & choice) != 0;
        private static string Hex(float hue, float saturation, float value) =>
            "#" + ColorUtility.ToHtmlStringRGB(Color.HSVToRGB(Mathf.Repeat(hue, 1f), saturation, value));

        private static int Seed(int seed, string text)
        {
            foreach (char character in text ?? "")
                seed = unchecked(seed * 31 + character);
            return seed;
        }
    }
}
