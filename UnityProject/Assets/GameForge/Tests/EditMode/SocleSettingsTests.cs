using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace GameForge.Tests.EditMode
{
    public class SocleSettingsTests
    {
        private const string ScenePath = "Assets/GameForge/Setup/Smoke.unity";
        private const string PipelinePath = "Assets/GameForge/Setup/Rendering/Mobile_RPAsset.asset";

        [Test]
        public void MobilePipelineIsTheDefault()
        {
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);

            Assert.That(pipeline, Is.Not.Null);
            Assert.That(GraphicsSettings.defaultRenderPipeline, Is.SameAs(pipeline));
            Assert.That(QualitySettings.renderPipeline, Is.SameAs(pipeline));
            Assert.That(QualitySettings.names, Is.EqualTo(new[] { "Mobile" }));
            Assert.That(pipeline.scriptableRenderer, Is.Not.Null);
            Assert.That(pipeline.supportsHDR, Is.False);
            Assert.That(pipeline.supportsCameraDepthTexture, Is.False);
            Assert.That(pipeline.supportsCameraOpaqueTexture, Is.False);
        }

        [Test]
        public void SmokeSceneIsRegisteredAndInputSystemIsEnabled()
        {
            Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath), Is.Not.Null);
            Assert.That(EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path),
                Is.EqualTo(new[] { ScenePath }));
#if !ENABLE_INPUT_SYSTEM
            Assert.Fail("Le nouveau système d’entrée doit être actif.");
#endif
        }
    }
}
