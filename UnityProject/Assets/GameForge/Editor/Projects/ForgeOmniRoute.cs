using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace GameForge.Editor
{
    internal sealed class ForgeOmniRoute
    {
        internal const string DefaultBaseUrl = "http://localhost:20128/v1";
        internal const string ApiKeyVariable = "GAMEFORGE_OMNIROUTE_API_KEY";
        internal const int MaxResponseBytes = 128 * 1024;
        private const int MaxPromptLength = 60000;
        private const int MaxTextLength = 32000;
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);
        private static readonly HttpClient SharedClient = new HttpClient(new HttpClientHandler
        {
            AllowAutoRedirect = false,
            UseCookies = false,
            UseProxy = false
        }) { Timeout = Timeout.InfiniteTimeSpan };

        private readonly HttpClient client;
        private readonly Func<string> readKey;
        private readonly TimeSpan timeout;

        public ForgeOmniRoute()
            : this(SharedClient, () => Environment.GetEnvironmentVariable(ApiKeyVariable), TimeSpan.FromSeconds(60))
        {
        }

        internal ForgeOmniRoute(HttpClient client, Func<string> readKey, TimeSpan timeout)
        {
            this.client = client ?? throw new ArgumentNullException(nameof(client));
            this.readKey = readKey ?? throw new ArgumentNullException(nameof(readKey));
            this.timeout = timeout;
        }

        public async Task<string> CompleteAsync(string baseUrl, string model, string instruction, string prompt,
            CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();
            var endpoint = ResolveEndpoint(baseUrl);
            if (string.IsNullOrWhiteSpace(model) || model.Length > 256 || model.Any(char.IsControl))
                throw new ArgumentException("Indique le nom du modèle OmniRoute.", nameof(model));
            if (string.IsNullOrWhiteSpace(instruction) || instruction.Length > 12000 ||
                string.IsNullOrWhiteSpace(prompt) || prompt.Length > MaxPromptLength)
                throw new ArgumentException("Le texte à envoyer à OmniRoute est vide ou trop long.");

            string key = readKey();
            if (string.IsNullOrEmpty(key) || key.Length > 1024 || key.Any(character => character < 33 || character > 126))
                throw new InvalidOperationException($"Configure {ApiKeyVariable} avec une clé OmniRoute valide.");

            var payload = new
            {
                model = model.Trim(),
                messages = new[] { new { role = "system", content = instruction }, new { role = "user", content = prompt } },
                stream = false,
                max_tokens = 2300
            };
            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");

            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
            deadline.CancelAfter(timeout);
            try
            {
                using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, deadline.Token)
                    .ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                    throw new InvalidOperationException($"OmniRoute a refusé la requête (HTTP {(int)response.StatusCode}).");

                string text = await ReadBodyAsync(response.Content, deadline.Token).ConfigureAwait(false);
                return ParseCompletion(text);
            }
            catch (OperationCanceledException) when (!cancellation.IsCancellationRequested)
            {
                throw new TimeoutException("OmniRoute a dépassé le délai de réponse.");
            }
            catch (Exception exception) when (exception is HttpRequestException or IOException)
            {
                cancellation.ThrowIfCancellationRequested();
                if (deadline.IsCancellationRequested)
                    throw new TimeoutException("OmniRoute a dépassé le délai de réponse.");
                throw new InvalidOperationException("Impossible de recevoir la réponse d’OmniRoute.");
            }
            catch (Exception exception) when (exception is JsonException or DecoderFallbackException)
            {
                throw new InvalidDataException("OmniRoute a retourné une réponse JSON illisible.");
            }
        }

        private static Uri ResolveEndpoint(string baseUrl)
        {
            if (string.IsNullOrWhiteSpace(baseUrl) || baseUrl.Length > 2048 || baseUrl.Any(char.IsControl) ||
                !Uri.TryCreate(baseUrl.Trim(), UriKind.Absolute, out var root) ||
                string.IsNullOrEmpty(root.Host) || root.UserInfo.Length != 0 || root.Query.Length != 0 || root.Fragment.Length != 0 ||
                !root.AbsolutePath.TrimEnd('/').EndsWith("/v1", StringComparison.Ordinal))
                throw new ArgumentException("Indique une URL OmniRoute qui se termine par /v1, sans identifiant ni paramètres.", nameof(baseUrl));

            var endpoint = new UriBuilder(root) { Path = root.AbsolutePath.TrimEnd('/') + "/chat/completions" };
            if (root.Scheme == Uri.UriSchemeHttps)
                return endpoint.Uri;
            if (root.Scheme == Uri.UriSchemeHttp)
            {
                if (string.Equals(root.Host, "localhost", StringComparison.OrdinalIgnoreCase))
                    endpoint.Host = IPAddress.Loopback.ToString();
                if (IPAddress.TryParse(endpoint.Host.Trim('[', ']'), out var address) && IPAddress.IsLoopback(address))
                    return endpoint.Uri;
            }
            throw new ArgumentException("OmniRoute exige HTTPS en dehors de cette machine.", nameof(baseUrl));
        }

        private static async Task<string> ReadBodyAsync(HttpContent content, CancellationToken cancellation)
        {
            if (content == null)
                throw new InvalidDataException("OmniRoute n’a renvoyé aucun contenu.");
            if (content.Headers.ContentLength > MaxResponseBytes)
                throw new InvalidDataException("La réponse d’OmniRoute dépasse la taille autorisée.");

            using var stream = await content.ReadAsStreamAsync().ConfigureAwait(false);
            using var body = new MemoryStream();
            var bytes = new byte[4096];
            int count;
            while ((count = await stream.ReadAsync(bytes, 0, bytes.Length, cancellation).ConfigureAwait(false)) != 0)
            {
                if (body.Length + count > MaxResponseBytes)
                    throw new InvalidDataException("La réponse d’OmniRoute dépasse la taille autorisée.");
                body.Write(bytes, 0, count);
            }
            cancellation.ThrowIfCancellationRequested();
            return Utf8.GetString(body.GetBuffer(), 0, (int)body.Length);
        }

        private static string ParseCompletion(string json)
        {
            using var input = new StringReader(json);
            using var reader = new JsonTextReader(input) { MaxDepth = 16, DateParseHandling = DateParseHandling.None };
            var document = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
            if (reader.Read() || document["choices"] is not JArray choices || choices.Count == 0 ||
                choices[0] is not JObject choice || choice["message"] is not JObject message ||
                message["content"]?.Type != JTokenType.String)
                throw new InvalidDataException("La réponse d’OmniRoute ne contient aucun texte exploitable.");

            var finish = choice["finish_reason"];
            var text = (string)message["content"];
            if ((finish != null && (finish.Type != JTokenType.String || (string)finish != "stop")) ||
                string.IsNullOrWhiteSpace(text) || text.Length > MaxTextLength)
                throw new InvalidDataException("La réponse d’OmniRoute est incomplète ou trop longue.");
            return text;
        }
    }
}
