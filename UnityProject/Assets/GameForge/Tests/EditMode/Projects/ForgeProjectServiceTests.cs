using System;
using System.Collections;
using System.IO;
using System.Linq;
using GameForge.Editor;
using GameForge.Runtime;
using GameForge.Tests.EditMode.Generation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace GameForge.Tests.EditMode.Projects
{
    public sealed class ForgeProjectServiceTests
    {
        private string root;
        private RunnerCatalog catalog;
        private ForgeProjectService service;
        private Guid generatedId;

        [SetUp]
        public void SetUp()
        {
            root = Path.Combine(Path.GetTempPath(), "GameForgeService-" + Guid.NewGuid().ToString("N"));
            catalog = RunnerGenerationTests.LoadCatalog();
            Assert.That(catalog, Is.Not.Null);
            service = new ForgeProjectService(new ForgeProjectStore(root), catalog);
            generatedId = Guid.Empty;
        }

        [TearDown]
        public void TearDown()
        {
            if (generatedId != Guid.Empty)
                RunnerGenerationTests.DeleteFixture(generatedId);
            if (!Directory.Exists(root))
                return;
            foreach (string path in Directory.EnumerateFiles(root))
                File.Delete(path);
            Directory.Delete(root);
        }

        [Test]
        public void OnlyAcceptingGameplayPublishesTheRealVariant()
        {
            var originalFolders = PublishedFolders();
            var draft = service.Create();
            DelegateRequiredQuestions(draft);
            Assert.That(draft.stage, Is.EqualTo(ForgeStage.Idea));
            Assert.Throws<InvalidOperationException>(() => service.ProposeLooks(draft));
            Assert.Throws<InvalidOperationException>(() => service.StartQuestions(draft));

            draft.title = "Parcours du crépuscule";
            draft.idea = "Des obstacles colorés sur une piste au crépuscule.";
            service.StartQuestions(draft);
            Assert.That(draft.stage, Is.EqualTo(ForgeStage.Questions));
            service.ProposeLooks(draft);
            Assert.That(draft.stage, Is.EqualTo(ForgeStage.Appearance));
            Assert.Throws<InvalidOperationException>(() => service.Generate(draft, 0));
            service.ChooseLook(draft, 2);
            Assert.That(draft.stage, Is.EqualTo(ForgeStage.Gameplay));
            Assert.That(PublishedFolders(), Is.EquivalentTo(originalFolders));
            Assert.That(draft.variantId, Is.Null.Or.Empty);
            Assert.That(draft.scenePath, Is.Null.Or.Empty);

            const int acceptedIndex = 1;
            var expected = draft.playStyles[acceptedIndex].config;
            var result = service.Generate(draft, acceptedIndex);
            Guid.TryParseExact(result.VariantId, "N", out generatedId);
            var variant = RunnerGenerationTests.ReadGenerated(new GenerationStore(), result);
            Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(variant.ScenePath), Is.Not.Null);
            var settings = AssetDatabase.LoadAssetAtPath<RunnerSettings>(variant.SettingsPath);
            Assert.That(settings, Is.Not.Null);
            Assert.That(JsonUtility.ToJson(settings.ToConfig()), Is.EqualTo(JsonUtility.ToJson(expected)));
            var saved = service.Read(draft.id);
            Assert.That(saved.stage, Is.EqualTo(ForgeStage.Complete));
            Assert.That(saved.chosenPlay, Is.EqualTo(acceptedIndex));
            Assert.That(saved.variantId, Is.EqualTo(variant.Id));
            Assert.That(saved.scenePath, Is.EqualTo(variant.ScenePath));

            saved.stage = ForgeStage.Gameplay;
            saved.scenePath = null;
            service.Save(saved);
            Assert.That(service.Generate(service.Read(saved.id), acceptedIndex).VariantId, Is.EqualTo(variant.Id));
            Assert.That(PublishedFolders().Except(originalFolders).Count(), Is.EqualTo(1));
        }

        [Test]
        public void DetailedBriefPersistsAndChangesTheSceneInputs()
        {
            var draft = service.Create();
            draft.start = ForgeStart.Choose;
            service.StartQuestions(draft);
            Assert.That(() => service.ProposeLooks(draft), Throws.InvalidOperationException);
            DelegateRequiredQuestions(draft);
            draft.brief.Set(ForgePromptId.World, "Un tunnel nocturne, avec des panneaux mats.");
            draft.brief.Set(ForgePromptId.Enemy, "Un drone vert à deux rotors.");
            draft.brief.SetLocked(ForgePromptId.Enemy, true);
            draft.brief.Set(ForgePromptId.Goal, "Survivre 45 secondes.");
            draft.brief.SetLocked(ForgePromptId.Goal, true);
            draft.brief.Set(ForgePromptId.Encounters, "2 obstacles par segment.");
            draft.brief.SetLocked(ForgePromptId.Encounters, true);
            draft.brief.Set(ForgePromptId.Progression, "Un début tranquille.");
            draft.brief.SetLocked(ForgePromptId.Progression, true);
            draft.briefChapter = 2;
            draft.briefQuestion = 2;
            service.Save(draft);

            draft = service.Read(draft.id);
            Assert.That(draft.briefChapter, Is.EqualTo(2));
            Assert.That(draft.briefQuestion, Is.EqualTo(2));
            Assert.That(draft.brief.Get(ForgePromptId.Enemy), Is.EqualTo("Un drone vert à deux rotors."));
            Assert.That(draft.brief.IsLocked(ForgePromptId.Enemy), Is.True);
            service.ProposeLooks(draft);
            Assert.That(draft.looks.Select(look => look.config.enemyDesign), Is.All.EqualTo(RunnerEnemyDesign.Drone));
            Assert.That(draft.looks.Select(look => look.config.themeId), Is.All.EqualTo("theme-night"));
            Assert.That(draft.looks.Select(look => look.detail), Is.All.Matches<string>(text => text.Contains("tunnel nocturne")));
            service.ChooseLook(draft, 1);
            service.RegenerateGameplay(draft);
            var saved = service.Read(draft.id);
            Assert.That(saved.playStyles.Select(play => play.config.enemyDesign), Is.All.EqualTo(RunnerEnemyDesign.Drone));
            Assert.That(saved.playStyles.Select(play => play.config.objective), Is.All.EqualTo(RunnerObjective.Endurance));
            Assert.That(saved.playStyles.Select(play => play.config.timeLimitSeconds), Is.All.EqualTo(45));
            Assert.That(saved.playStyles.Select(play => play.config.obstacleDensity), Is.All.EqualTo(2));
            Assert.That(saved.playStyles.Select(play => play.config.initialSpeed), Is.All.EqualTo(5f));
        }

        [Test]
        public void OmniRouteRequiresFourDistinctLabels()
        {
            const string valid = "{\"proposals\":[{\"label\":\"Premier\"},{\"label\":\"Second\"},{\"label\":\"Troisième\"},{\"label\":\"Quatrième\"}]}";
            Assert.That(ForgeProjectService.ReadAiLabels(valid), Has.Length.EqualTo(4));
            Assert.Throws<InvalidDataException>(() => ForgeProjectService.ReadAiLabels(valid.Replace("Second", "Premier")));
            Assert.Throws<InvalidDataException>(() => ForgeProjectService.ReadAiLabels("{\"proposals\":[]}"));
        }

        [UnityTest]
        public IEnumerator AtelierRestoresActiveChapterAndKeepsTheTwoSeriesInOrder()
        {
            var window = ScriptableObject.CreateInstance<EditorWindow>();
            var panel = new ForgeProjectPanel(service, _ => Assert.Fail("Aucun jeu ne doit encore être créé."), () => { });
            window.Show();
            try
            {
                window.rootVisualElement.Add(panel);
                yield return null;
                panel.StartProject();
                Click(panel, "forge-project-start-choose");
                Click(panel, "forge-project-questionnaire");
                Click(panel, "forge-project-brief-delegate-Concept");
                Click(panel, "forge-project-brief-delegate-Player");
                Click(panel, "forge-project-chapter-1");
                Click(panel, "forge-project-brief-delegate-World");
                Click(panel, "forge-project-chapter-2");
                const string drone = "Un drone vert à deux rotors.";
                var answer = panel.Q<TextField>("forge-project-brief-Enemy");
                answer.value = drone;
                panel.Q<Toggle>("forge-project-brief-lock-Enemy").value = true;
                Assert.That(panel.Q<Button>("forge-project-brief-Enemy-microphone"), Is.Not.Null);

                var saved = service.List().Single();
                saved.briefQuestion = 3;
                service.Save(saved);
                panel.ShowProjects();
                Click(panel, "forge-project-resume-" + saved.id);
                Assert.That(panel.Q<TextField>("forge-project-brief-Enemy").value, Is.EqualTo(drone));
                Assert.That(panel.Q<Toggle>("forge-project-brief-lock-Enemy").value, Is.True);
                Click(panel, "forge-project-next-chapter");
                Assert.That(service.Read(saved.id).briefQuestion, Is.Zero);
                Click(panel, "forge-project-brief-delegate-Goal");
                Click(panel, "forge-project-next-chapter");
                Click(panel, "forge-project-propose-looks");
                for (int i = 0; i < 4; i++)
                    Assert.That(panel.Q<Button>("forge-project-look-" + i), Is.Not.Null);
                Click(panel, "forge-project-look-2");
                for (int i = 0; i < 4; i++)
                    Assert.That(panel.Q<Button>("forge-project-gameplay-" + i), Is.Not.Null);
                Assert.That(panel.Q<Button>("forge-project-generate").enabledInHierarchy, Is.False);
                Assert.That(service.Read(saved.id).variantId, Is.Null.Or.Empty);
            }
            finally
            {
                window.Close();
            }
        }

        private static void Click(VisualElement panel, string id)
        {
            var button = panel.Q<Button>(id);
            Assert.That(button, Is.Not.Null, id);
            Assert.That(button.enabledInHierarchy, Is.True, id);
            using var submit = NavigationSubmitEvent.GetPooled();
            submit.target = button;
            button.SendEvent(submit);
        }

        [Test]
        public void RegenerationMakesFourNewDistinctPalettes()
        {
            var draft = StartProject();
            service.ProposeLooks(draft);
            var first = draft.looks.Select(look => Palette(look.config)).ToArray();
            Assert.That(first.Length, Is.EqualTo(4));
            Assert.That(first.Distinct().Count(), Is.EqualTo(4));

            service.ProposeLooks(draft, regenerate: true);
            var second = draft.looks.Select(look => Palette(look.config)).ToArray();
            Assert.That(second.Length, Is.EqualTo(4));
            Assert.That(second.Distinct().Count(), Is.EqualTo(4));
            Assert.That(second, Is.Not.EquivalentTo(first));
            Assert.That(draft.lookRound, Is.EqualTo(1));
            Assert.That(service.Read(draft.id).looks.Select(look => Palette(look.config)), Is.EqualTo(second));
        }

        [Test]
        public void DefaultChoicesAndRegenerationKeepAnObstacleChallengeInEveryMode()
        {
            var draft = StartProject();
            service.ProposeLooks(draft);
            service.ChooseLook(draft, 0);
            var first = draft.playStyles.Select(play => play.config).ToArray();
            service.RegenerateGameplay(draft);
            var next = draft.playStyles.Select(play => play.config).ToArray();

            Assert.That(first.Concat(next).Select(config => config.obstacleDensity), Is.All.GreaterThan(0));
            Assert.That(next.Select(config => config.objective), Is.EquivalentTo(Enum.GetValues(typeof(RunnerObjective))));
            Assert.That(next.Select(config => config.seed).Intersect(first.Select(config => config.seed)), Is.Empty);
        }

        [Test]
        public void RegeneratingGameplayAfterReopeningKeepsTheChosenPaletteAndEveryLock()
        {
            var draft = StartProject();
            draft.locks = ForgeLocks.Theme | ForgeLocks.Pace | ForgeLocks.Density | ForgeLocks.Obstacle | ForgeLocks.Objective;
            var answers = draft.answers;
            answers.themeId = "theme-night";
            answers.initialSpeed = 7f;
            answers.maxSpeed = 13f;
            answers.acceleration = 0.2f;
            answers.obstacleDensity = 2;
            answers.obstacleIds = new[] { "obstacle-barrier", "obstacle-block" };
            answers.objective = RunnerObjective.Sprint;
            answers.goalDistance = 120;
            answers.timeLimitSeconds = 25;
            service.ProposeLooks(draft);
            Assert.That(draft.looks.Select(look => look.config.themeId), Is.All.EqualTo(answers.themeId));
            var expectedPalette = Palette(draft.looks[2].config);
            service.ChooseLook(draft, 2);
            AssertLockedPlayStyles(draft, expectedPalette);
            var firstSeeds = draft.playStyles.Select(play => play.config.seed).ToArray();

            var reopened = new ForgeProjectService(new ForgeProjectStore(root), catalog);
            var saved = reopened.Read(draft.id);
            Assert.That(saved.stage, Is.EqualTo(ForgeStage.Gameplay));
            Assert.That(saved.locks, Is.EqualTo(draft.locks));
            Assert.That(saved.chosenLook, Is.EqualTo(2));
            AssertLockedPlayStyles(saved, expectedPalette);
            reopened.RegenerateGameplay(saved);
            AssertLockedPlayStyles(saved, expectedPalette);
            Assert.That(saved.playRound, Is.EqualTo(1));
            Assert.That(saved.playStyles.Select(play => play.config.seed), Is.Not.EquivalentTo(firstSeeds));
        }

        private ForgeProject StartProject()
        {
            var draft = service.Create();
            DelegateRequiredQuestions(draft);
            draft.title = "Piste du soir";
            draft.idea = "Une course avec des obstacles lumineux.";
            service.StartQuestions(draft);
            return draft;
        }

        private static void DelegateRequiredQuestions(ForgeProject draft)
        {
            foreach (var prompt in ForgeBrief.Chapters.SelectMany(chapter => chapter.Prompts).Where(prompt => prompt.Required))
                draft.brief.Set(prompt.Id, ForgeBrief.DelegateAnswer);
        }

        private static void AssertLockedPlayStyles(ForgeProject draft, string expectedPalette)
        {
            Assert.That(draft.playStyles.Length, Is.EqualTo(4));
            Assert.That(draft.playStyles.Select(play => play.config.seed).Distinct().Count(), Is.EqualTo(4));
            var answers = draft.answers;
            foreach (var proposal in draft.playStyles)
            {
                var config = proposal.config;
                Assert.That(Palette(config), Is.EqualTo(expectedPalette));
                Assert.That(config.themeId, Is.EqualTo(answers.themeId));
                Assert.That(config.initialSpeed, Is.EqualTo(answers.initialSpeed));
                Assert.That(config.maxSpeed, Is.EqualTo(answers.maxSpeed));
                Assert.That(config.acceleration, Is.EqualTo(answers.acceleration));
                Assert.That(config.obstacleDensity, Is.EqualTo(answers.obstacleDensity));
                Assert.That(config.obstacleIds, Is.EqualTo(answers.obstacleIds));
                Assert.That(config.objective, Is.EqualTo(answers.objective));
                Assert.That(config.goalDistance, Is.EqualTo(answers.goalDistance));
                Assert.That(config.timeLimitSeconds, Is.EqualTo(answers.timeLimitSeconds));
            }
        }

        private static string Palette(RunnerConfigV1 config) =>
            string.Join("/", config.skyColor, config.trackColor, config.playerColor, config.obstacleColor);

        private static string[] PublishedFolders() => AssetDatabase.IsValidFolder(GenerationStore.GeneratedRoot)
            ? AssetDatabase.GetSubFolders(GenerationStore.GeneratedRoot)
                .Where(path => Guid.TryParseExact(Path.GetFileName(path), "N", out _)).ToArray()
            : Array.Empty<string>();
    }
}
