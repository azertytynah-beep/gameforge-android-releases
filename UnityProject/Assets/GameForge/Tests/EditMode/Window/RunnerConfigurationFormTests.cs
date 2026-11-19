using System.Collections;
using System.Linq;
using GameForge.Editor;
using GameForge.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace GameForge.Tests.EditMode.Window
{
    public sealed class RunnerConfigurationFormTests
    {
        [UnityTest]
        public IEnumerator ToolsMenuOpensAResizableWindowWithKeyboardFocusableControls()
        {
            var existing = Resources.FindObjectsOfTypeAll<GameForgeWindow>();
            Assert.That(EditorApplication.ExecuteMenuItem("Tools/GameForge"), Is.True);
            yield return null;

            var window = Resources.FindObjectsOfTypeAll<GameForgeWindow>().FirstOrDefault();
            Assert.That(window, Is.Not.Null);
            var originalPosition = window.position;
            try
            {
                window.CreateGUI();
                window.position = new Rect(originalPosition.position, window.minSize);
                var form = window.rootVisualElement.Q<RunnerConfigurationForm>();
                Assert.That(form, Is.Not.Null);
                Assert.That(form.Q<ScrollView>("configuration-fields").mode, Is.EqualTo(ScrollViewMode.Vertical));
                Assert.That(form.Q<DropdownField>("templateId").value, Is.EqualTo("Endless Runner 3D"));
                Assert.That(form.Q<TextField>("title").focusable, Is.True);
                Assert.That(form.Q<Button>("generate").focusable, Is.True);

                form.Bind(new FixtureCatalog(), new[] { "theme-default" }, new[] { "obstacle-block" }, new RecordingGenerator());
                var generate = form.Q<Button>("generate");
                Assert.That(generate.enabledSelf, Is.True);
                var speed = form.Q<TextField>("maxSpeed");
                speed.value = "22";
                yield return null;

                Assert.That(generate.enabledSelf, Is.False);
                Assert.That(form.Q<HelpBox>("maxSpeed-error").text, Does.Contain("18"));
            }
            finally
            {
                if (existing.Contains(window))
                    window.position = originalPosition;
                else
                    window.Close();
            }
        }

        [Test]
        public void UnboundFormMakesItClearGenerationIsUnavailable()
        {
            var form = new RunnerConfigurationForm();
            Assert.That(form.Q<Button>("generate").enabledSelf, Is.False);
            Assert.That(form.Q<HelpBox>("services-status").text, Does.Contain("catalogue"));
            Assert.That(form.TryGenerate(), Is.False);
        }

        [Test]
        public void InvalidFieldsKeepRawInputAndDoNotCallTheGenerator()
        {
            var (form, generator) = CreateBoundForm();
            form.Q<TextField>("title").value = "Ma variante";
            form.Q<TextField>("seed").value = "4294967296";
            form.Q<TextField>("initialSpeed").value = "?";
            form.Q<Toggle>("obstacle-obstacle-block").value = false;

            Assert.That(form.TryGenerate(), Is.False);
            Assert.That(form.Q<Button>("generate").enabledSelf, Is.False);
            Assert.That(generator.Configuration, Is.Null);
            Assert.That(form.Q<HelpBox>("seed-error").text, Does.Contain("32 bits"));
            Assert.That(form.Q<HelpBox>("initialSpeed-error").text, Does.Contain("fini"));
            Assert.That(form.Q<HelpBox>("obstacleIds-error").text, Does.Contain("au moins un"));
            Assert.That(form.Q<TextField>("title").value, Is.EqualTo("Ma variante"));
            Assert.That(form.Q<TextField>("initialSpeed").value, Is.EqualTo("?"));
        }

        [Test]
        public void ValidInputReachesTheGeneratorAndDisplaysTheResult()
        {
            var (form, generator) = CreateBoundForm();
            form.Q<TextField>("title").value = "Ma variante";
            form.Q<TextField>("seed").value = "-2147483648";
            form.Q<TextField>("initialSpeed").value = "7,5";
            form.Q<TextField>("maxSpeed").value = "16";
            form.Q<TextField>("acceleration").value = "0.25";
            form.Q<TextField>("obstacleDensity").value = "2";
            form.Q<DropdownField>("themeId").value = "theme-forest";
            form.Q<Toggle>("obstacle-obstacle-block").value = false;
            form.Q<Toggle>("obstacle-arch").value = true;

            Assert.That(form.TryGenerate(), Is.True);
            var configuration = generator.Configuration;
            Assert.That(configuration.schemaVersion, Is.EqualTo(1));
            Assert.That(configuration.templateId, Is.EqualTo("runner-3d"));
            Assert.That(configuration.title, Is.EqualTo("Ma variante"));
            Assert.That(configuration.seed, Is.EqualTo(int.MinValue));
            Assert.That(configuration.initialSpeed, Is.EqualTo(7.5f));
            Assert.That(configuration.maxSpeed, Is.EqualTo(16f));
            Assert.That(configuration.acceleration, Is.EqualTo(0.25f));
            Assert.That(configuration.obstacleDensity, Is.EqualTo(2));
            Assert.That(configuration.themeId, Is.EqualTo("theme-forest"));
            Assert.That(configuration.obstacleIds, Is.EqualTo(new[] { "arch" }));
            Assert.That(form.Q<HelpBox>("generation-result").text, Does.Contain("var-test"));
        }

        [Test]
        public void EditingLoadedVariantKeepsItsPaletteAndObjective()
        {
            var (form, generator) = CreateBoundForm();
            var settings = RunnerConfigV1.CreateDefault();
            settings.skyColor = "#112233";
            settings.trackColor = "#224433";
            settings.objective = RunnerObjective.Endurance;
            settings.timeLimitSeconds = 45;
            form.Load(settings);
            form.Q<TextField>("seed").value = "123";

            Assert.That(form.TryGenerate(), Is.True);
            Assert.That(generator.Configuration.seed, Is.EqualTo(123));
            Assert.That(generator.Configuration.skyColor, Is.EqualTo(settings.skyColor));
            Assert.That(generator.Configuration.trackColor, Is.EqualTo(settings.trackColor));
            Assert.That(generator.Configuration.objective, Is.EqualTo(settings.objective));
            Assert.That(generator.Configuration.timeLimitSeconds, Is.EqualTo(45));
        }

        [Test]
        public void GeneratorFailureIsShownWithoutLosingTheForm()
        {
            var (form, generator) = CreateBoundForm();
            generator.Result = new GenerationResult(null, null, new[] { new RunnerDiagnostic("$", "Échec de sauvegarde.") });
            form.Q<TextField>("title").value = "Essai";

            Assert.That(form.TryGenerate(), Is.False);
            Assert.That(form.Q<HelpBox>("generation-result").messageType, Is.EqualTo(HelpBoxMessageType.Error));
            Assert.That(form.Q<HelpBox>("generation-result").text, Is.EqualTo("Échec de sauvegarde."));
            Assert.That(form.Q<TextField>("title").value, Is.EqualTo("Essai"));
        }

        private static (RunnerConfigurationForm Form, RecordingGenerator Generator) CreateBoundForm()
        {
            var generator = new RecordingGenerator();
            var form = new RunnerConfigurationForm();
            form.Bind(new FixtureCatalog(), new[] { "theme-default", "theme-forest" },
                new[] { "obstacle-block", "arch" }, generator);
            return (form, generator);
        }

        private sealed class FixtureCatalog : IRunnerCatalog
        {
            public bool HasTheme(string id) => id == "theme-default" || id == "theme-forest";
            public bool HasObstacle(string id) => id == "obstacle-block" || id == "arch";
        }

        private sealed class RecordingGenerator : IGameGenerator
        {
            public RunnerConfigV1 Configuration { get; private set; }
            public GenerationResult Result { get; set; } = new GenerationResult("var-test", "Assets/Generated/Test.unity", null);

            public GenerationResult Generate(RunnerConfigV1 configuration)
            {
                Configuration = configuration;
                return Result;
            }
        }
    }
}
