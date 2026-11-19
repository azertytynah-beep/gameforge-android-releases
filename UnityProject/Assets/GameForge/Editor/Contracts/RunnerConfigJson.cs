using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using GameForge.Runtime;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace GameForge.Editor
{
    public static class RunnerConfigJson
    {
        public const int MaxUtf8Bytes = 8192;

        private static readonly HashSet<string> Fields = new HashSet<string>(StringComparer.Ordinal)
        {
            "schemaVersion", "templateId", "title", "seed", "initialSpeed", "maxSpeed",
            "acceleration", "obstacleDensity", "themeId", "obstacleIds",
            "skyColor", "trackColor", "playerColor", "obstacleColor", "objective", "goalDistance", "timeLimitSeconds"
        };

        public static bool TryParse(string json, IRunnerCatalog catalog, out RunnerConfigV1 configuration,
            out IReadOnlyList<RunnerDiagnostic> diagnostics)
        {
            configuration = null;
            if (string.IsNullOrWhiteSpace(json) || json.Length > MaxUtf8Bytes || Encoding.UTF8.GetByteCount(json) > MaxUtf8Bytes)
            {
                diagnostics = new[] { new RunnerDiagnostic("$", "Fournir un objet JSON de 8 Kio au maximum.") };
                return false;
            }

            JObject root;
            try
            {
                using var input = new StringReader(json);
                using var reader = new JsonTextReader(input) { DateParseHandling = DateParseHandling.None, MaxDepth = 8 };
                root = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                if (reader.Read())
                {
                    diagnostics = new[] { new RunnerDiagnostic("$", "Un seul objet JSON est autorisé.") };
                    return false;
                }
            }
            catch (JsonException)
            {
                diagnostics = new[] { new RunnerDiagnostic("$", "Objet JSON invalide ou champ répété.") };
                return false;
            }

            var errors = new List<RunnerDiagnostic>();
            foreach (var property in root.Properties())
            {
                if (!Fields.Contains(property.Name))
                    errors.Add(new RunnerDiagnostic(property.Name, "Champ inconnu."));
            }

            var draft = new RunnerConfigV1
            {
                schemaVersion = ReadInteger(root, "schemaVersion", errors),
                templateId = ReadString(root["templateId"], "templateId", errors),
                title = ReadString(root["title"], "title", errors),
                seed = ReadInteger(root, "seed", errors),
                initialSpeed = ReadNumber(root, "initialSpeed", errors),
                maxSpeed = ReadNumber(root, "maxSpeed", errors),
                acceleration = ReadNumber(root, "acceleration", errors),
                obstacleDensity = ReadInteger(root, "obstacleDensity", errors),
                themeId = ReadString(root["themeId"], "themeId", errors),
                obstacleIds = ReadObstacles(root["obstacleIds"], errors),
                skyColor = ReadOptionalString(root["skyColor"], "skyColor", errors),
                trackColor = ReadOptionalString(root["trackColor"], "trackColor", errors),
                playerColor = ReadOptionalString(root["playerColor"], "playerColor", errors),
                obstacleColor = ReadOptionalString(root["obstacleColor"], "obstacleColor", errors),
                objective = (RunnerObjective)ReadOptionalInteger(root, "objective", errors),
                goalDistance = ReadOptionalInteger(root, "goalDistance", errors),
                timeLimitSeconds = ReadOptionalInteger(root, "timeLimitSeconds", errors)
            };

            diagnostics = errors.Count == 0 ? RunnerConfigValidator.Validate(draft, catalog) : errors;
            if (diagnostics.Count != 0)
                return false;

            configuration = draft;
            return true;
        }

        private static int ReadInteger(JObject root, string field, List<RunnerDiagnostic> errors)
        {
            var token = root[field];
            if (token?.Type == JTokenType.Integer &&
                int.TryParse(token.ToString(Formatting.None), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int value))
                return value;

            errors.Add(new RunnerDiagnostic(field, "Entier signé sur 32 bits requis."));
            return 0;
        }

        private static int ReadOptionalInteger(JObject root, string field, List<RunnerDiagnostic> errors) =>
            root[field] == null ? 0 : ReadInteger(root, field, errors);

        private static float ReadNumber(JObject root, string field, List<RunnerDiagnostic> errors)
        {
            var token = root[field];
            if (token is JValue number && (token.Type == JTokenType.Integer || token.Type == JTokenType.Float) &&
                float.TryParse(Convert.ToString(number.Value, CultureInfo.InvariantCulture), NumberStyles.Float,
                    CultureInfo.InvariantCulture, out float value) && !float.IsNaN(value) && !float.IsInfinity(value))
                return value;

            errors.Add(new RunnerDiagnostic(field, "Nombre fini requis."));
            return 0;
        }

        private static string ReadString(JToken token, string field, List<RunnerDiagnostic> errors)
        {
            if (token?.Type == JTokenType.String)
                return token.Value<string>();

            errors.Add(new RunnerDiagnostic(field, "Chaîne de caractères requise."));
            return null;
        }

        private static string ReadOptionalString(JToken token, string field, List<RunnerDiagnostic> errors) =>
            token == null || token.Type == JTokenType.Null ? null : ReadString(token, field, errors);

        private static string[] ReadObstacles(JToken token, List<RunnerDiagnostic> errors)
        {
            if (!(token is JArray array) || array.Count > RunnerConfigValidator.MaxObstacleTypes)
            {
                errors.Add(new RunnerDiagnostic("obstacleIds", "Tableau de 16 identifiants au maximum requis."));
                return null;
            }

            var result = new string[array.Count];
            for (int i = 0; i < result.Length; i++)
                result[i] = ReadString(array[i], $"obstacleIds[{i}]", errors);
            return result;
        }
    }
}
