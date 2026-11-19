using System.Threading;
using System.Threading.Tasks;
using GameForge.Editor;
using NUnit.Framework;

namespace GameForge.Tests.EditMode
{
    public sealed class ForgeLocalIdeasTests
    {
        [Test]
        public async Task UsesThreeOfflineIdeasWhenNoModelIsSelected()
        {
            var result = await ForgeLocalIdeas.SuggestAsync("", "", CancellationToken.None);
            Assert.That(result.FromAi, Is.False);
            Assert.That(result.Ideas, Has.Length.EqualTo(3));
        }

        [Test]
        public void ValidatesInstalledModelAndBoundedStructuredIdeas()
        {
            Assert.That(ForgeLocalIdeas.CanRequest("runner:cloud"), Is.False);
            Assert.That(ForgeLocalIdeas.CanRequest("https://example.com/model"), Is.False);
            Assert.That(ForgeLocalIdeas.CanRequest("qwen3:0.6b"), Is.True);
            const string tags = @"{
                ""models"": [
                    { ""name"": ""runner:local"", ""size"": 432109876, ""details"": { ""format"": ""gguf"" } },
                    { ""name"": ""runner:stub"", ""size"": 12, ""details"": { ""format"": ""gguf"" } },
                    { ""name"": ""runner:alias"", ""size"": 432109876, ""details"": { ""format"": ""gguf"" },
                      ""remote_model"": ""remote:cloud"" }
                ]
            }";
            Assert.That(ForgeLocalIdeas.IsInstalledLocalModel(tags, "runner:local"), Is.True);
            Assert.That(ForgeLocalIdeas.IsInstalledLocalModel(tags, "runner:stub"), Is.False);
            Assert.That(ForgeLocalIdeas.IsInstalledLocalModel(tags, "runner:alias"), Is.False);

            const string valid = "{\"done\":true,\"response\":\"{\\\"ideas\\\":[\\\"Piste en forêt\\\",\\\"Sprint en forêt\\\",\\\"Course lunaire\\\"]}\"}";
            Assert.That(ForgeLocalIdeas.ParseIdeas(valid), Has.Length.EqualTo(3));
            Assert.That(ForgeLocalIdeas.ParseIdeas(valid.Replace("Sprint en forêt", "Piste en forêt")), Is.Null);
        }
    }
}
