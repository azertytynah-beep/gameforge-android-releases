using System;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using GameForge.Runtime;
using UnityEngine;

namespace GameForge.Editor
{
    internal sealed partial class ForgeProjectService
    {
        private static readonly string[] LookDirections = { "Signal", "Horizon", "Contraste", "Silhouette" };

        private void ApplyBrief(ForgeProject draft, RunnerConfigV1 config)
        {
            var brief = draft.brief;
            if (brief?.detailed != true)
                return;
            int missing = brief.MissingChapter;
            if (missing >= 0)
                throw new InvalidOperationException($"Complète le chapitre « {ForgeBrief.Chapters[missing].Title} » ou laisse GameForge proposer pour les questions requises.");

            foreach (var entry in brief.entries.OrderBy(entry => entry.id))
                config.seed = Seed(config.seed, entry.text);

            if (!Locked(draft, ForgeLocks.Theme) && FixedLighting(brief))
                config.themeId = IsNight(brief) ? "theme-night" : "theme-default";

            string enemy = Fold(brief.Get(ForgePromptId.Enemy));
            config.enemyDesign = EnemyFor(enemy);
            if (NoEnemies(brief))
            {
                if (brief.IsLocked(ForgePromptId.Enemy) && Locked(draft, ForgeLocks.Density) && config.obstacleDensity > 0)
                    throw new InvalidOperationException("Le brief impose une piste sans ennemi et la densité impose des obstacles. Ajuste un de ces verrous.");
                if (!Locked(draft, ForgeLocks.Density))
                    config.obstacleDensity = 0;
            }
            else if (!Locked(draft, ForgeLocks.Density))
            {
                config.obstacleDensity = Measure(Fold(brief.Get(ForgePromptId.Encounters)), @"obstacles?", config.obstacleDensity, 1, 3);
            }

            if (!Locked(draft, ForgeLocks.Pace))
            {
                string pace = Fold(brief.Get(ForgePromptId.Progression));
                if (HasWord(pace, "doucement", "calme", "lent", "tranquille"))
                    config.initialSpeed = Math.Min(config.initialSpeed, 5f);
                if (HasWord(pace, "rapide", "accelere", "intense"))
                    config.acceleration = Math.Max(config.acceleration, .22f);
            }
            if (!Locked(draft, ForgeLocks.Objective))
                ApplyGoal(brief.Get(ForgePromptId.Goal), config);
        }

        private static RunnerEnemyDesign EnemyFor(string enemy)
        {
            if (HasWord(enemy, "drone", "drones", "rotor", "rotors", "aerien"))
                return RunnerEnemyDesign.Drone;
            if (HasWord(enemy, "robot", "robots", "androide", "sentinelle"))
                return RunnerEnemyDesign.Robot;
            if (HasWord(enemy, "plante", "plantes", "liane", "champignon", "cactus"))
                return RunnerEnemyDesign.Plant;
            if (HasWord(enemy, "golem", "rocher", "rochers", "pierre", "cristal"))
                return RunnerEnemyDesign.Golem;
            if (HasWord(enemy, "creature", "monstre", "animal", "loup", "dragon", "renard", "insecte", "chouette", "araignee"))
                return RunnerEnemyDesign.Creature;
            return RunnerEnemyDesign.Catalog;
        }

        private static void ApplyGoal(string raw, RunnerConfigV1 config)
        {
            string goal = Fold(raw);
            int distance = Measure(goal, @"m|metres?", 100, 10, 3000);
            int seconds = Measure(goal, @"s|secondes?", 30, 5, 180);
            if (HasWord(goal, "sprint", "chrono", "chronometre") || (HasWord(goal, "temps") && distance != 100))
            {
                config.objective = RunnerObjective.Sprint;
                config.timeLimitSeconds = seconds;
                config.goalDistance = Math.Min(distance, (int)DistanceInTime(config));
            }
            else if (HasWord(goal, "survivre", "endurance", "tenir"))
            {
                config.objective = RunnerObjective.Endurance;
                config.timeLimitSeconds = seconds;
                config.goalDistance = 0;
            }
            else if (HasWord(goal, "atteindre", "terminer", "arrivee", "metre", "metres"))
            {
                config.objective = RunnerObjective.Distance;
                config.goalDistance = distance;
                config.timeLimitSeconds = 0;
            }
        }

        private static int Measure(string value, string unit, int fallback, int min, int max)
        {
            var match = Regex.Match(value, @"\b(\d{1,5})\s*(?:" + unit + @")\b");
            return match.Success && int.TryParse(match.Groups[1].Value, out int parsed) ? Mathf.Clamp(parsed, min, max) : fallback;
        }

        private static bool NoEnemies(ForgeBrief brief)
        {
            string enemy = Fold(brief?.Get(ForgePromptId.Enemy));
            return enemy is "aucun" or "aucun ennemi" or "aucun obstacle" or "sans ennemi" or "sans obstacle"
                || enemy.StartsWith("aucun ennemi ", StringComparison.Ordinal) || enemy.StartsWith("sans obstacle ", StringComparison.Ordinal);
        }

        private static bool FixedLighting(ForgeBrief brief) =>
            IsNight(brief) || HasWord(Fold(brief?.Get(ForgePromptId.Atmosphere)), "matin", "aube", "jour", "soleil");

        private static bool IsNight(ForgeBrief brief) =>
            HasWord(Fold(brief?.Get(ForgePromptId.World) + " " + brief?.Get(ForgePromptId.Atmosphere)), "nuit", "nocturne", "minuit");

        private static bool FixedObjective(ForgeProject draft) =>
            Locked(draft, ForgeLocks.Objective) || draft.brief?.IsLocked(ForgePromptId.Goal) == true;

        private static bool FixedDensity(ForgeProject draft) =>
            Locked(draft, ForgeLocks.Density) || draft.brief?.IsLocked(ForgePromptId.Encounters) == true || NoEnemies(draft.brief);

        private static bool FixedPace(ForgeProject draft) =>
            Locked(draft, ForgeLocks.Pace) || draft.brief?.IsLocked(ForgePromptId.Progression) == true;

        private static float? FindHue(string text)
        {
            string search = Fold(text);
            if (HasWord(search, "rose", "magenta")) return .91f;
            if (HasWord(search, "violet", "mauve", "pourpre")) return .76f;
            if (HasWord(search, "bleu", "bleue", "cyan")) return .56f;
            if (HasWord(search, "turquoise")) return .48f;
            if (HasWord(search, "vert", "verte", "jade")) return .34f;
            if (HasWord(search, "jaune", "or", "dore")) return .15f;
            if (HasWord(search, "orange", "ambre")) return .08f;
            if (HasWord(search, "rouge", "carmin")) return .99f;
            return null;
        }

        private static string Fold(string text)
        {
            if (string.IsNullOrEmpty(text))
                return "";
            var folded = new StringBuilder(text.Length);
            foreach (char character in text.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD))
                if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
                    folded.Append(character);
            return folded.ToString();
        }

        private static bool HasWord(string value, params string[] words) =>
            Regex.IsMatch(value, @"\b(?:" + string.Join("|", words) + @")\b", RegexOptions.CultureInvariant);

        private static string LookLabel(int index, int round, string palette, bool night) =>
            LookDirections[(index + round % LookDirections.Length) % LookDirections.Length] + " " + palette +
            (night ? " · Nuit" : "");

        private static void DescribeLook(ForgeBrief brief, ForgeProposal proposal, int index, int round)
        {
            var detail = new[]
            {
                "Le danger ressort au premier plan pour faciliter les décisions rapides sur mobile.",
                "La couleur de l’horizon guide le regard au bout des trois voies.",
                "Le sol calme contraste avec la silhouette du joueur et les obstacles.",
                "Le héros et le danger restent séparés par leur forme et leur couleur."
            }[(index + round % ProposalCount) % ProposalCount];
            string universe = BriefExcerpt(brief, ForgePromptId.World);
            proposal.detail = string.IsNullOrEmpty(universe) ? detail : $"{detail} Univers demandé · {universe}";
            proposal.enemyStyle = EnemyLabel(proposal.config.enemyDesign);
            proposal.assetNote = AssetNote(brief, proposal.config);
        }

        private static string DescribeGameplay(ForgeBrief brief, RunnerConfigV1 play, int variation)
        {
            string tactic = play.obstacleDensity switch
            {
                0 => "Teste le rythme de la course et les changements de voie sur une piste dégagée.",
                1 => "Lis l’obstacle devant toi et change de voie quand la sortie est visible.",
                _ => "Repère la voie ouverte avant chaque groupe d’obstacles et anticipe le virage suivant."
            };
            string end = play.objective is RunnerObjective.Distance or RunnerObjective.Sprint
                ? "La partie se termine à l’objectif, au contact d’un danger ou à l’expiration du chrono s’il existe."
                : "Le contact met fin à la partie. Le bouton Rejouer relance le même parcours.";
            string feedback = variation % 2 == 0 ? "Le HUD suit ta distance." : "Le HUD affiche ta progression en haut de l’écran.";
            string request = BriefExcerpt(brief, ForgePromptId.Goal);
            return $"{tactic} {feedback} {end}" +
                (string.IsNullOrEmpty(request) ? "" : $" Objectif demandé · {request}");
        }

        private static string EnemyLabel(RunnerEnemyDesign design) => design switch
        {
            RunnerEnemyDesign.Robot => "Robot low-poly",
            RunnerEnemyDesign.Creature => "Créature à oreilles pointues",
            RunnerEnemyDesign.Golem => "Golem minéral",
            RunnerEnemyDesign.Drone => "Drone à deux rotors",
            RunnerEnemyDesign.Plant => "Plante massive",
            _ => "Obstacles du catalogue"
        };

        private static string AssetNote(ForgeBrief brief, RunnerConfigV1 config)
        {
            if (config.obstacleDensity == 0)
                return "Piste sans obstacle.";
            bool original = config.enemyDesign != RunnerEnemyDesign.Catalog;
            string note = original
                ? "Silhouette 3D originale incluse, construite localement à la génération du parcours. Collision sur une voie."
                : "Blocs ou barrières originaux inclus dans le catalogue.";
            string requested = BriefExcerpt(brief, ForgePromptId.Enemy, 140);
            return string.IsNullOrEmpty(requested) ? note : $"{note} Description conservée · {requested}";
        }

        private static string BriefExcerpt(ForgeBrief brief, ForgePromptId id, int limit = 180)
        {
            var text = (brief?.Get(id) ?? "").Trim();
            if (text == ForgeBrief.DelegateAnswer)
                return "";
            return text.Length <= limit ? text : text.Substring(0, limit) + "…";
        }
    }
}
