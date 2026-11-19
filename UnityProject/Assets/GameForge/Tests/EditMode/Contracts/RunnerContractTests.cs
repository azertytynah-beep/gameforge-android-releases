using System.Linq;
using GameForge.Editor;
using GameForge.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace GameForge.Tests.EditMode
{
    public sealed class RunnerContractTests
    {
        private const string FixturePath = "Assets/GameForge/Tests/EditMode/Contracts/Fixtures/";
        private readonly IRunnerCatalog catalog = new FixtureCatalog();

        [Test]
        public void ValidFixtureProducesIndependentSettings()
        {
            Assert.That(RunnerConfigJson.TryParse(Fixture("RunnerValid"), catalog, out var config, out var errors),
                Is.True, string.Join(", ", errors.Select(error => error.Field)));
            Assert.That(RunnerConfigValidator.TryCreateSettings(config, catalog, out var settings, out _), Is.True);
            try
            {
                config.obstacleIds[0] = "another";
                settings.ToConfig().obstacleIds[0] = "another";
                Assert.That(settings.ObstacleIds.Single(), Is.EqualTo("obstacle-block"));
                Assert.That(settings.InitialSpeed, Is.EqualTo(6f));
            }
            finally
            {
                Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void InvalidFixtureNamesEachRejectedField()
        {
            Assert.That(RunnerConfigJson.TryParse(Fixture("RunnerInvalid"), catalog, out var config, out var errors), Is.False);
            Assert.That(config, Is.Null);
            Assert.That(errors.Select(error => error.Field), Is.SupersetOf(new[] { "schemaVersion", "initialSpeed", "maxSpeed", "themeId" }));
        }

        [TestCase(4f, 4f, 0f, 0, int.MinValue)]
        [TestCase(12f, 18f, 0.5f, 3, int.MaxValue)]
        public void BoundsAndSignedSeedsAreAccepted(float initial, float max, float acceleration, int density, int seed)
        {
            var config = RunnerConfigV1.CreateDefault();
            config.seed = seed;
            config.initialSpeed = initial;
            config.maxSpeed = max;
            config.acceleration = acceleration;
            config.obstacleDensity = density;
            if (density == 0)
                config.obstacleIds = new string[0];
            Assert.That(RunnerConfigJson.TryParse(JsonUtility.ToJson(config), catalog, out var parsed, out _), Is.True);
            Assert.That(parsed.seed, Is.EqualTo(seed));
            Assert.That(parsed.maxSpeed, Is.EqualTo(max));
        }

        [TestCase("seed", "\"12\"")]
        [TestCase("seed", "2147483648")]
        [TestCase("seed", "-2147483649")]
        [TestCase("seed", "1.0")]
        [TestCase("initialSpeed", "NaN")]
        [TestCase("initialSpeed", "Infinity")]
        [TestCase("initialSpeed", "1e300")]
        [TestCase("obstacleDensity", "null")]
        public void RejectsInvalidJsonTypesAndNumbers(string field, string replacement)
        {
            var input = Fixture("RunnerValid");
            var original = field == "initialSpeed" ? "6" : field == "seed" ? "0" : "1";
            input = input.Replace($"\"{field}\":{original}", $"\"{field}\":{replacement}");
            Assert.That(RunnerConfigJson.TryParse(input, catalog, out _, out var errors), Is.False);
            Assert.That(errors.Select(error => error.Field), Does.Contain(field));
        }

        [TestCase(",\"extra\":true}", "extra")]
        [TestCase(",\"seed\":4}", "$")]
        [TestCase("} {}", "$")]
        public void RejectsUnknownDuplicateAndTrailingValues(string suffix, string field)
        {
            var input = Fixture("RunnerValid").TrimEnd();
            input = input.Substring(0, input.Length - 1) + suffix;
            Assert.That(RunnerConfigJson.TryParse(input, catalog, out _, out var errors), Is.False);
            Assert.That(errors.Select(error => error.Field), Does.Contain(field));
        }

        [Test]
        public void RejectsMissingFieldsAndPayloadsOverUtf8Limit()
        {
            Assert.That(RunnerConfigJson.TryParse("{}", catalog, out _, out var errors), Is.False);
            Assert.That(errors.Select(error => error.Field), Does.Contain("seed"));
            var large = "{\"title\":\"" + new string('é', RunnerConfigJson.MaxUtf8Bytes / 2) + "\"}";
            Assert.That(RunnerConfigJson.TryParse(large, catalog, out _, out errors), Is.False);
            Assert.That(errors.Single().Field, Is.EqualTo("$"));
        }

        [Test]
        public void ManualValidationRejectsInvalidMechanicsAndLocalReferences()
        {
            var config = RunnerConfigV1.CreateDefault();
            config.acceleration = float.NaN;
            config.maxSpeed = 5;
            config.obstacleIds = new[] { "https://example.org" };
            Assert.That(RunnerConfigValidator.TryCreateSettings(config, catalog, out var settings, out var errors), Is.False);
            Assert.That(settings, Is.Null);
            Assert.That(errors.Select(error => error.Field),
                Is.SupersetOf(new[] { "acceleration", "maxSpeed", "obstacleIds[0]" }));
        }

        private static string Fixture(string name) => AssetDatabase.LoadAssetAtPath<TextAsset>(FixturePath + name + ".json").text;

        private sealed class FixtureCatalog : IRunnerCatalog
        {
            public bool HasTheme(string id) => id == "theme-default";
            public bool HasObstacle(string id) => id == "obstacle-block";
        }
    }
}
