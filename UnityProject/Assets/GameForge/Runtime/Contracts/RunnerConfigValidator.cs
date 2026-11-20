using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace GameForge.Runtime
{
    public static class RunnerConfigValidator
    {
        public const int MaxTitleLength = 60;
        public const int MaxObstacleTypes = 16;
        public const int MaxGoalDistance = 100000;
        public const int MaxTimeLimitSeconds = 3600;

        private static readonly Regex CatalogId = new Regex(@"\A[a-z0-9][a-z0-9-]{0,47}\z", RegexOptions.CultureInvariant);
        private static readonly Regex HexColor = new Regex(@"\A#[0-9a-fA-F]{6}\z", RegexOptions.CultureInvariant);

        public static IReadOnlyList<RunnerDiagnostic> Validate(RunnerConfigV1 config, IRunnerCatalog catalog)
        {
            if (config == null)
                return new[] { new RunnerDiagnostic("$", "Une configuration est requise.") };
            if (catalog == null)
                return new[] { new RunnerDiagnostic("$", "Un catalogue local est requis.") };

            var errors = new List<RunnerDiagnostic>();
            if (config.schemaVersion != RunnerConfigV1.Version)
                errors.Add(new RunnerDiagnostic("schemaVersion", "Version de configuration non prise en charge."));
            if (config.templateId != RunnerConfigV1.RunnerTemplateId)
                errors.Add(new RunnerDiagnostic("templateId", "Sélectionner le modèle runner-3d."));
            if (string.IsNullOrWhiteSpace(config.title) || config.title.Length > MaxTitleLength ||
                Array.Exists(config.title.ToCharArray(), char.IsControl))
                errors.Add(new RunnerDiagnostic("title", "Utiliser de 1 à 60 caractères imprimables."));

            if (!Within(config.initialSpeed, 4f, 12f))
                errors.Add(new RunnerDiagnostic("initialSpeed", "Vitesse initiale attendue entre 4 et 12 m/s."));
            if (!Within(config.maxSpeed, 4f, 18f) || config.maxSpeed < config.initialSpeed)
                errors.Add(new RunnerDiagnostic("maxSpeed", "La vitesse maximale doit atteindre la vitesse initiale et rester au plus à 18 m/s."));
            if (!Within(config.acceleration, 0f, 0.5f))
                errors.Add(new RunnerDiagnostic("acceleration", "Accélération attendue entre 0 et 0,5 m/s²."));
            if (config.obstacleDensity < 0 || config.obstacleDensity > 3)
                errors.Add(new RunnerDiagnostic("obstacleDensity", "Densité attendue entre 0 et 3."));

            if (!IsCatalogId(config.themeId) || !catalog.HasTheme(config.themeId))
                errors.Add(new RunnerDiagnostic("themeId", "Thème inconnu dans le catalogue local."));

            if (!Enum.IsDefined(typeof(RunnerObjective), config.objective))
                errors.Add(new RunnerDiagnostic("objective", "Objectif de partie inconnu."));
            if (!Enum.IsDefined(typeof(RunnerEnemyDesign), config.enemyDesign))
                errors.Add(new RunnerDiagnostic("enemyDesign", "Silhouette d’ennemi inconnue."));
            if ((config.objective is RunnerObjective.Distance or RunnerObjective.Sprint) &&
                (config.goalDistance < 1 || config.goalDistance > MaxGoalDistance))
                errors.Add(new RunnerDiagnostic("goalDistance", $"Distance attendue entre 1 et {MaxGoalDistance} m."));
            if ((config.objective is RunnerObjective.Sprint or RunnerObjective.Endurance) &&
                (config.timeLimitSeconds < 1 || config.timeLimitSeconds > MaxTimeLimitSeconds))
                errors.Add(new RunnerDiagnostic("timeLimitSeconds", $"Durée attendue entre 1 et {MaxTimeLimitSeconds} s."));

            ValidateColor(config.skyColor, "skyColor", errors);
            ValidateColor(config.trackColor, "trackColor", errors);
            ValidateColor(config.playerColor, "playerColor", errors);
            ValidateColor(config.obstacleColor, "obstacleColor", errors);

            var obstacles = config.obstacleIds;
            if (obstacles == null || obstacles.Length > MaxObstacleTypes || (config.obstacleDensity > 0 && obstacles.Length == 0))
            {
                errors.Add(new RunnerDiagnostic("obstacleIds", "Sélectionner jusqu’à 16 obstacles et au moins un pour une densité positive."));
                return errors;
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < obstacles.Length; i++)
            {
                var id = obstacles[i];
                if (!IsCatalogId(id) || !catalog.HasObstacle(id) || !seen.Add(id))
                    errors.Add(new RunnerDiagnostic($"obstacleIds[{i}]", "Obstacle inconnu ou déjà sélectionné."));
            }

            return errors;
        }

#if !GAMEFORGE_DESKTOP
        public static bool TryCreateSettings(RunnerConfigV1 config, IRunnerCatalog catalog,
            out RunnerSettings settings, out IReadOnlyList<RunnerDiagnostic> diagnostics)
        {
            diagnostics = Validate(config, catalog);
            settings = diagnostics.Count == 0 ? RunnerSettings.FromValidatedConfig(config) : null;
            return settings != null;
        }

#endif
        private static bool Within(float value, float min, float max) => value >= min && value <= max;

        private static bool IsCatalogId(string value) => value != null && value.Length <= 48 && CatalogId.IsMatch(value);

        private static void ValidateColor(string color, string field, List<RunnerDiagnostic> errors)
        {
            if (!string.IsNullOrEmpty(color) && !HexColor.IsMatch(color))
                errors.Add(new RunnerDiagnostic(field, "Couleur attendue au format #RRGGBB."));
        }
    }
}
