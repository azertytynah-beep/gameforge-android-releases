using System;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace GameForge.Editor
{
    internal static class AndroidBuildJournal
    {
        private const int FormatVersion = 1;

        public static void Run(string path, AndroidBuildSettings planned, Func<AndroidBuildSettings> capture,
            Action<AndroidBuildSettings> apply, Action build)
        {
            Begin(path, capture(), planned);
            try
            {
                apply(planned);
                if (!JToken.DeepEquals(JObject.FromObject(capture()), JObject.FromObject(planned)))
                    throw new InvalidOperationException("Les réglages Android attendus n’ont pas tous été appliqués.");
                build();
            }
            finally
            {
                Recover(path, capture, apply);
            }
        }

        public static void Begin(string path, AndroidBuildSettings previous, AndroidBuildSettings planned)
        {
            var journal = new JObject
            {
                ["version"] = FormatVersion,
                ["previous"] = JObject.FromObject(previous),
                ["planned"] = JObject.FromObject(planned)
            };
            var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    var json = Encoding.UTF8.GetBytes(journal.ToString(Formatting.Indented));
                    file.Write(json, 0, json.Length);
                    file.Flush(true);
                }
                File.Move(temporary, path);
            }
            finally
            {
                if (File.Exists(temporary))
                    File.Delete(temporary);
            }
        }

        public static void Recover(string path, Func<AndroidBuildSettings> capture, Action<AndroidBuildSettings> apply)
        {
            if (!File.Exists(path))
                return;

            var (previous, planned) = Read(path);
            var current = JObject.FromObject(capture());
            if (previous.Count != current.Count || planned.Count != current.Count)
                throw new InvalidDataException("Le journal Android est incomplet. Il a été conservé.");
            foreach (var field in current.Properties())
            {
                if (!previous.TryGetValue(field.Name, out var original) ||
                    !planned.TryGetValue(field.Name, out var intended))
                    throw new InvalidDataException("Le journal Android est incomplet. Il a été conservé.");
                if (!JToken.DeepEquals(field.Value, original) && !JToken.DeepEquals(field.Value, intended))
                    throw new InvalidOperationException(
                        $"Le réglage Android « {field.Name} » a changé en dehors de l’export. Journal conservé.");
            }

            if (!JToken.DeepEquals(current, previous))
                apply(previous.ToObject<AndroidBuildSettings>());
            if (!JToken.DeepEquals(JObject.FromObject(capture()), previous))
                throw new InvalidOperationException("La restauration des réglages Android est incomplète. Journal conservé.");

            File.Delete(path);
        }

        private static (JObject previous, JObject planned) Read(string path)
        {
            try
            {
                if (new FileInfo(path).Length > 64 * 1024)
                    throw new InvalidDataException("Le journal Android est trop volumineux. Il a été conservé.");
                var journal = JObject.Parse(File.ReadAllText(path), new JsonLoadSettings
                {
                    DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error
                });
                if (journal["version"]?.Type != JTokenType.Integer || (int)journal["version"] != FormatVersion ||
                    !(journal["previous"] is JObject previous) || !(journal["planned"] is JObject planned) ||
                    !SameFields(previous, planned))
                    throw new InvalidDataException("Le journal Android est invalide. Il a été conservé.");
                return (previous, planned);
            }
            catch (JsonException exception)
            {
                throw new InvalidDataException("Le journal Android est illisible. Il a été conservé.", exception);
            }
        }

        private static bool SameFields(JObject previous, JObject planned) => previous.Count == planned.Count
            && previous.Properties().All(field => planned.TryGetValue(field.Name, out var intended) &&
                field.Value.Type == intended.Type);
    }
}
