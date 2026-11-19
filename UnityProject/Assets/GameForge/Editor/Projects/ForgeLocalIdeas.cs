using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace GameForge.Editor
{
    internal sealed class ForgeIdeaSuggestions
    {
        public string[] Ideas { get; }
        public bool FromAi { get; }

        public ForgeIdeaSuggestions(string[] ideas, bool fromAi)
        {
            Ideas = ideas;
            FromAi = fromAi;
        }
    }

    internal static class ForgeLocalIdeas
    {
        private const int MaxResponseBytes = 64 * 1024;
        private static readonly HttpClient Client = new HttpClient(new HttpClientHandler
        {
            UseProxy = false,
            AllowAutoRedirect = false
        }) { BaseAddress = new Uri("http://127.0.0.1:11434/") };
        private static readonly Regex LocalName = new Regex(
            @"\A[a-zA-Z0-9_][a-zA-Z0-9_.-]*(?::[a-zA-Z0-9_.-]+)?\z", RegexOptions.CultureInvariant);
        private static readonly string[] StartingIdeas =
        {
            "Survivre dans les ruelles d’une ville nocturne aux barrières lumineuses.",
            "Atteindre la balise d’une réserve au lever du soleil, entre des rochers colorés.",
            "Traverser une piste de canyon avant la fin du chrono, entre des blocs rapprochés."
        };

        public static async Task<ForgeIdeaSuggestions> SuggestAsync(string model, string description, CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();
            if (CanRequest(model))
            {
                if (description?.Length > ForgeProjectStore.MaxIdeaLength)
                    description = description.Substring(0, ForgeProjectStore.MaxIdeaLength);
                using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
                deadline.CancelAfter(TimeSpan.FromSeconds(45));
                try
                {
                    using var tags = new HttpRequestMessage(HttpMethod.Get, "api/tags");
                    var installed = await ReadAsync(tags, deadline.Token).ConfigureAwait(false);
                    string exactName = model.Contains(":") ? model : model + ":latest";
                    if (IsInstalledLocalModel(installed, exactName))
                    {
                        var payload = new
                        {
                            model = exactName,
                            system = "Propose trois concepts distincts en français pour un runner 3D portrait à trois voies. " +
                                "Chaque concept tient en une phrase de 140 caractères et décrit un univers avec un objectif de course. " +
                                "Les objectifs disponibles sont distance, sprint, endurance et sans fin. " +
                                "Les commandes sont deux boutons gauche et droite. Réponds uniquement avec le JSON demandé.",
                            prompt = string.IsNullOrWhiteSpace(description) ? "Propose librement trois idées." : description,
                            stream = false,
                            keep_alive = 0,
                            options = new { num_predict = 400, num_ctx = 2048 },
                            format = new
                            {
                                type = "object",
                                properties = new { ideas = new { type = "array", minItems = 3, maxItems = 3,
                                    items = new { type = "string", maxLength = 160 } } },
                                required = new[] { "ideas" },
                                additionalProperties = false
                            }
                        };
                        using var request = new HttpRequestMessage(HttpMethod.Post, "api/generate")
                        {
                            Content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json")
                        };
                        var response = await ReadAsync(request, deadline.Token).ConfigureAwait(false);
                        var ideas = ParseIdeas(response);
                        if (ideas != null)
                            return new ForgeIdeaSuggestions(ideas, fromAi: true);
                    }
                }
                catch (OperationCanceledException) when (!cancellation.IsCancellationRequested) { }
                catch (Exception exception) when (exception is HttpRequestException or IOException or JsonException or
                    DecoderFallbackException or InvalidCastException or OverflowException or FormatException) { }
            }
            cancellation.ThrowIfCancellationRequested();
            return new ForgeIdeaSuggestions((string[])StartingIdeas.Clone(), fromAi: false);
        }

        internal static bool CanRequest(string model) =>
            !string.IsNullOrEmpty(model) && model.Length <= 128 && LocalName.IsMatch(model) &&
            model.IndexOf("cloud", StringComparison.OrdinalIgnoreCase) < 0;

        internal static bool IsInstalledLocalModel(string tags, string exactName) =>
            ParseObject(tags)["models"] is JArray models && models.OfType<JObject>().Any(item =>
                item["name"] is JValue { Type: JTokenType.String } && (string)item["name"] == exactName &&
                item["details"] is JObject details && details["format"] is JValue { Type: JTokenType.String } &&
                (string)details["format"] == "gguf" && item["size"]?.Type == JTokenType.Integer &&
                (long)item["size"] >= 1024 * 1024 && item["remote_model"] == null);

        internal static string[] ParseIdeas(string json)
        {
            var envelope = ParseObject(json);
            if (envelope["done"]?.Type != JTokenType.Boolean || !(bool)envelope["done"] ||
                envelope["response"]?.Type != JTokenType.String || ((string)envelope["response"]).Length > 4096)
                return null;
            var response = ParseObject((string)envelope["response"]);
            if (response.Count != 1 || response["ideas"] is not JArray ideas || ideas.Count != 3 ||
                ideas.Any(item => item.Type != JTokenType.String))
                return null;
            var values = ideas.Values<string>().Select(idea => idea.Trim()).ToArray();
            return values.All(idea => idea.Length is >= 5 and <= 160 && !idea.Any(char.IsControl)) &&
                values.Distinct(StringComparer.OrdinalIgnoreCase).Count() == 3 ? values : null;
        }

        private static async Task<string> ReadAsync(HttpRequestMessage request, CancellationToken cancellation)
        {
            using var response = await Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellation)
                .ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
            using var body = new MemoryStream();
            var buffer = new byte[4096];
            int count;
            while ((count = await stream.ReadAsync(buffer, 0, buffer.Length, cancellation).ConfigureAwait(false)) != 0)
            {
                if (body.Length + count > MaxResponseBytes)
                    throw new IOException("La réponse d’Ollama dépasse la taille attendue.");
                body.Write(buffer, 0, count);
            }
            return new UTF8Encoding(false, true).GetString(body.ToArray());
        }

        private static JObject ParseObject(string value)
        {
            using var input = new StringReader(value);
            using var reader = new JsonTextReader(input) { MaxDepth = 12, DateParseHandling = DateParseHandling.None };
            var result = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
            if (reader.Read())
                throw new JsonReaderException("Un seul objet JSON est attendu.");
            return result;
        }
    }
}
