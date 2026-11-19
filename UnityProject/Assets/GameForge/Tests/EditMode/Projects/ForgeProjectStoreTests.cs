using System;
using System.IO;
using System.Linq;
using GameForge.Editor;
using GameForge.Runtime;
using NUnit.Framework;

namespace GameForge.Tests.EditMode.Projects
{
    public sealed class ForgeProjectStoreTests
    {
        private string root;
        private ForgeProjectStore store;

        [SetUp]
        public void SetUp()
        {
            root = Path.Combine(Path.GetTempPath(), "GameForge-" + Guid.NewGuid().ToString("N"));
            store = new ForgeProjectStore(root);
        }

        [TearDown]
        public void TearDown()
        {
            if (!Directory.Exists(root))
                return;
            foreach (string path in Directory.EnumerateFiles(root))
                File.Delete(path);
            Directory.Delete(root);
        }

        [Test]
        public void ResumesDraftAndPendingVariantAfterReopeningTheStore()
        {
            var draft = store.Create();
            Assert.That(draft.DisplayTitle, Is.EqualTo("Sans titre"));
            Assert.That(store.Read(draft.id).stage, Is.EqualTo(ForgeStage.Idea));

            draft.title = "Sentier sous la lune";
            draft.idea = "Une course dans la nuit.\nDes obstacles espacés.";
            draft.start = ForgeStart.Explore;
            draft.stage = ForgeStage.Gameplay;
            draft.locks = ForgeLocks.Theme | ForgeLocks.Objective;
            draft.answers.seed = 42;
            draft.answers.skyColor = "#010203";
            draft.answers.objective = RunnerObjective.Distance;
            draft.answers.goalDistance = 75;
            draft.looks = Enumerable.Range(0, 4).Select(_ =>
                new ForgeProposal { label = "Nocturne", summary = "Un ciel étoilé", config = draft.answers.Copy() }).ToArray();
            draft.playStyles = draft.looks;
            draft.lookRound = 2;
            draft.chosenLook = 0;
            draft.chosenPlay = 1;
            draft.variantId = Guid.NewGuid().ToString("N");
            store.Save(draft);

            var reopened = new ForgeProjectStore(root);
            var pending = reopened.Read(draft.id);
            Assert.That(pending.DisplayTitle, Is.EqualTo(draft.title));
            Assert.That(pending.idea, Is.EqualTo(draft.idea));
            Assert.That(pending.start, Is.EqualTo(ForgeStart.Explore));
            Assert.That(pending.stage, Is.EqualTo(ForgeStage.Gameplay));
            Assert.That(pending.locks, Is.EqualTo(draft.locks));
            Assert.That(pending.lookRound, Is.EqualTo(2));
            Assert.That(pending.chosenLook, Is.Zero);
            Assert.That(pending.chosenPlay, Is.EqualTo(1));
            Assert.That(pending.looks[0].config.seed, Is.EqualTo(42));
            Assert.That(pending.answers.skyColor, Is.EqualTo("#010203"));
            Assert.That(pending.answers.objective, Is.EqualTo(RunnerObjective.Distance));
            Assert.That(pending.answers.goalDistance, Is.EqualTo(75));
            Assert.That(pending.variantId, Is.EqualTo(draft.variantId));
            Assert.That(pending.scenePath, Is.Null.Or.Empty);

            pending.stage = ForgeStage.Complete;
            pending.scenePath = $"{GenerationStore.GeneratedRoot}/{pending.variantId}/Runner.unity";
            reopened.Save(pending);
            Assert.That(store.Read(draft.id).scenePath, Is.EqualTo(pending.scenePath));
            Assert.That(Directory.EnumerateFiles(root).Count(), Is.EqualTo(1));
        }

        [Test]
        public void InvalidIdOrScenePathDoesNotOverwriteTheSavedDraft()
        {
            var draft = store.Create();
            Assert.Throws<ArgumentException>(() => store.Read("../outside"));
            string path = Path.Combine(root, draft.id + ".json");
            string before = File.ReadAllText(path);

            draft.variantId = Guid.NewGuid().ToString("N");
            draft.scenePath = $"{GenerationStore.GeneratedRoot}/../Runner.unity";
            Assert.Throws<InvalidDataException>(() => store.Save(draft));
            draft.scenePath = null;
            draft.variantId = null;
            draft.stage = ForgeStage.Gameplay;
            Assert.Throws<InvalidDataException>(() => store.Save(draft));
            draft.stage = ForgeStage.Idea;
            draft.idea = new string('x', ForgeProjectStore.MaxIdeaLength + 1);
            Assert.Throws<InvalidDataException>(() => store.Save(draft));

            Assert.That(File.ReadAllText(path), Is.EqualTo(before));
            Assert.That(Directory.EnumerateFiles(root).Count(), Is.EqualTo(1));
        }

        [Test]
        public void ListSkipsCorruptRecordsAndKeepsDefaultsForAValidOlderDraft()
        {
            var created = store.Create();
            var legacyId = Guid.NewGuid().ToString("N");
            var damagedId = Guid.NewGuid().ToString("N");
            File.WriteAllText(Path.Combine(root, legacyId + ".json"),
                $"{{\"schemaVersion\":1,\"id\":\"{legacyId}\",\"title\":\"Brouillon\",\"legacyField\":true}}");
            File.WriteAllText(Path.Combine(root, damagedId + ".json"), "{");

            var projects = store.List();
            Assert.That(projects.Select(item => item.id), Is.EquivalentTo(new[] { created.id, legacyId }));
            var legacy = projects.Single(item => item.id == legacyId);
            Assert.That(legacy.stage, Is.EqualTo(ForgeStage.Idea));
            Assert.That(legacy.chosenLook, Is.EqualTo(-1));
            Assert.That(legacy.answers.obstacleIds, Is.Not.Empty);
            Assert.Throws<InvalidDataException>(() => store.Read(damagedId));
        }
    }
}
