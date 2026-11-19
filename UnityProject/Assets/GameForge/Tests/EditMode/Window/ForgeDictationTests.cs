using GameForge.Editor;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace GameForge.Tests.EditMode.Window
{
    public sealed class ForgeDictationTests
    {
        [TestCase(null, "  Une idée.  ", 30, "Une idée.")]
        [TestCase("Texte tapé", "suite dictée", 30, "Texte tapé suite dictée")]
        [TestCase("Texte tapé\n", "suite", 30, "Texte tapé\nsuite")]
        [TestCase("Texte tapé", "   ", 30, "Texte tapé")]
        [TestCase("Texte", "suite", 5, "Texte")]
        [TestCase("Texte", "suite", 9, "Texte sui")]
        [TestCase("Texte", "\ud83d\ude80", 7, "Texte")]
        public void AppendsFinalSpeechWithoutReplacingTypedText(string typed, string speech, int limit, string expected)
        {
            Assert.That(ForgeDictation.AppendTranscript(typed, speech, limit), Is.EqualTo(expected));
        }

        [Test]
        public void CreatingAndDisposingControlsLeavesTheTextEditable()
        {
            var field = new TextField { name = "description", value = "Description saisie" };
            using (var dictation = new ForgeDictation())
            {
                var controls = dictation.ButtonFor(field);
                Assert.That(controls.Q<Button>("description-microphone"), Is.Not.Null);
                dictation.Stop();
                Assert.That(field.value, Is.EqualTo("Description saisie"));
            }

            field.value += " à la main";
            Assert.That(field.value, Is.EqualTo("Description saisie à la main"));
        }
    }
}
