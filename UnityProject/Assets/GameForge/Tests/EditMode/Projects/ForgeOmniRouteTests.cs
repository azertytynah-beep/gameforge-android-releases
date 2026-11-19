using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using GameForge.Editor;
using NUnit.Framework;
using UnityEngine;

namespace GameForge.Tests.EditMode
{
    public sealed class ForgeOmniRouteTests
    {
        private const string TestKey = "test-placeholder-only";
        private const string BaseUrl = "https://gateway.invalid/v1";
        private const string Completion = @"{""choices"":[{""message"":{""content"":""{\""idea\"":\""Lune\""}""},""finish_reason"":""stop""}]}";

        [TestCase("https://gateway.invalid/v1", "https://gateway.invalid/v1/chat/completions")]
        [TestCase("http://localhost:20128/v1/", "http://127.0.0.1:20128/v1/chat/completions")]
        [TestCase("http://[::1]:20128/v1", "http://[::1]:20128/v1/chat/completions")]
        public async Task SendsOnlyTheSelectedBriefAndExtractsCompletionText(string baseUrl, string endpoint)
        {
            int requests = 0;
            using var http = new HttpClient(new Handler(async (request, _) =>
            {
                requests++;
                Assert.That(request.RequestUri.AbsoluteUri, Is.EqualTo(endpoint));
                Assert.That(request.Method, Is.EqualTo(HttpMethod.Post));
                Assert.That(request.Headers.Authorization.Scheme, Is.EqualTo("Bearer"));
                Assert.That(request.Headers.Authorization.Parameter, Is.EqualTo(TestKey));
                string json = await request.Content.ReadAsStringAsync().ConfigureAwait(false);
                var payload = JsonUtility.FromJson<Payload>(json);
                Assert.That(payload.model, Is.EqualTo("provider/model"));
                Assert.That(payload.stream, Is.False);
                Assert.That(payload.max_tokens, Is.EqualTo(2300));
                Assert.That(payload.messages, Has.Length.EqualTo(2));
                Assert.That(payload.messages[0].role, Is.EqualTo("system"));
                Assert.That(payload.messages[0].content, Is.EqualTo("Réponds en JSON."));
                Assert.That(payload.messages[1].role, Is.EqualTo("user"));
                Assert.That(payload.messages[1].content, Is.EqualTo("Mon jeu"));
                Assert.That(json, Does.Not.Contain(TestKey).And.Not.Contain("response_format"));
                return Reply(Completion);
            }));

            var result = await Create(http).CompleteAsync(baseUrl, "provider/model", "Réponds en JSON.", "Mon jeu",
                CancellationToken.None);
            Assert.That(result, Is.EqualTo("{\"idea\":\"Lune\"}"));
            Assert.That(requests, Is.EqualTo(1));
        }

        [TestCase("http://gateway.invalid/v1")]
        [TestCase("http://127.0.0.1.gateway.invalid/v1")]
        [TestCase("https://user:password@gateway.invalid/v1")]
        [TestCase("https://gateway.invalid/v1?token=placeholder")]
        [TestCase("https://gateway.invalid/v1#private")]
        [TestCase("https://gateway.invalid/v1/chat/completions")]
        public void RejectsUnsafeOrIncorrectEndpointBeforeSend(string baseUrl)
        {
            using var http = new HttpClient(new Handler((_, _) => throw new AssertionException("Unexpected HTTP")));
            Assert.ThrowsAsync<ArgumentException>(() => Complete(Create(http), baseUrl));
        }

        [Test]
        public void RefusesToSendWithoutTheEnvironmentKey()
        {
            using var http = new HttpClient(new Handler((_, _) => throw new AssertionException("Unexpected HTTP")));
            var client = new ForgeOmniRoute(http, () => null, TimeSpan.FromSeconds(1));
            var error = Assert.ThrowsAsync<InvalidOperationException>(() => Complete(client));
            Assert.That(error.Message, Does.Contain(ForgeOmniRoute.ApiKeyVariable));
        }

        [TestCase(HttpStatusCode.Redirect)]
        [TestCase(HttpStatusCode.Unauthorized)]
        [TestCase((HttpStatusCode)429)]
        public void RejectsStatusWithoutRevealingBodyOrKey(HttpStatusCode status)
        {
            int requests = 0;
            using var http = new HttpClient(new Handler((_, _) =>
            {
                requests++;
                var response = Reply("Provider log with " + TestKey, status);
                response.Headers.Location = new Uri("https://redirect.invalid/v1/chat/completions");
                return Task.FromResult(response);
            }));
            var error = Assert.ThrowsAsync<InvalidOperationException>(() => Complete(Create(http)));
            Assert.That(error.ToString(), Does.Not.Contain(TestKey).And.Not.Contain("redirect.invalid"));
            Assert.That(error.Message, Does.Contain(((int)status).ToString()));
            Assert.That(requests, Is.EqualTo(1));
        }

        [Test]
        public void StopsOversizedStreamingBodyWithoutContentLength()
        {
            using var http = new HttpClient(new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StreamContent(new UnseekableMemory(new byte[ForgeOmniRoute.MaxResponseBytes + 1]))
            })));
            var error = Assert.ThrowsAsync<InvalidDataException>(() => Complete(Create(http)));
            Assert.That(error.Message, Does.Contain("taille"));
        }

        [TestCase("{\"choices\":[],\"choices\":[]}")]
        [TestCase("{\"choices\":[{\"message\":{\"content\":\"{}\"},\"finish_reason\":\"length\"}]}")]
        public void RejectsMalformedOrTruncatedCompletion(string body)
        {
            using var http = new HttpClient(new Handler((_, _) => Task.FromResult(Reply(body))));
            Assert.ThrowsAsync<InvalidDataException>(() => Complete(Create(http)));
        }

        [Test]
        public void DistinguishesTimeoutFromUserCancellation()
        {
            using var timeoutHttp = new HttpClient(new Handler(async (_, token) =>
            {
                await Task.Delay(Timeout.Infinite, token).ConfigureAwait(false);
                throw new AssertionException("Unexpected completion");
            }));
            var timeout = new ForgeOmniRoute(timeoutHttp, () => TestKey, TimeSpan.FromMilliseconds(30));
            Assert.ThrowsAsync<TimeoutException>(() => Complete(timeout));

            using var user = new CancellationTokenSource();
            using var cancelHttp = new HttpClient(new Handler((_, _) =>
            {
                user.Cancel();
                return Task.FromCanceled<HttpResponseMessage>(user.Token);
            }));
            Assert.CatchAsync<OperationCanceledException>(() =>
                Create(cancelHttp).CompleteAsync(BaseUrl, "model", "Réponds en JSON.", "Mon jeu", user.Token));
        }

        private static HttpResponseMessage Reply(string body, HttpStatusCode status = HttpStatusCode.OK) =>
            new HttpResponseMessage(status) { Content = new StringContent(body) };

        private static Task<string> Complete(ForgeOmniRoute client, string baseUrl = BaseUrl) =>
            client.CompleteAsync(baseUrl, "model", "Réponds en JSON.", "Mon jeu", CancellationToken.None);

        private static ForgeOmniRoute Create(HttpClient client) =>
            new ForgeOmniRoute(client, () => TestKey, TimeSpan.FromSeconds(5));

        [Serializable]
        private sealed class Payload
        {
            public string model = null;
            public bool stream = false;
            public int max_tokens = 0;
            public Message[] messages = null;
        }

        [Serializable]
        private sealed class Message
        {
            public string role = null;
            public string content = null;
        }

        private sealed class Handler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send;

            public Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) => this.send = send;

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
                send(request, cancellationToken);
        }

        private sealed class UnseekableMemory : MemoryStream
        {
            public UnseekableMemory(byte[] bytes) : base(bytes) { }
            public override bool CanSeek => false;
        }
    }
}
