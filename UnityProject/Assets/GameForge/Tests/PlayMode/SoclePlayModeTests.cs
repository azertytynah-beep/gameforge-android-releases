using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace GameForge.Tests.PlayMode
{
    public class SoclePlayModeTests
    {
        private const string ScenePath = "Assets/GameForge/Setup/Smoke.unity";

        [UnityTest]
        public IEnumerator SmokeSceneRendersInPlayMode()
        {
            yield return SceneManager.LoadSceneAsync(ScenePath);

            Assert.That(Application.isPlaying, Is.True);
            var scene = SceneManager.GetActiveScene();
            Assert.That(scene.path, Is.EqualTo(ScenePath));

            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var component in root.GetComponentsInChildren<Component>(true))
                {
                    Assert.That(component != null, Is.True, $"Référence de composant manquante sur {root.name}.");
                }
            }

            var camera = Camera.main;
            Assert.That(camera, Is.Not.Null);
            Assert.That(GraphicsSettings.currentRenderPipeline, Is.TypeOf<UniversalRenderPipelineAsset>());

            var target = RenderTexture.GetTemporary(64, 64, 24, RenderTextureFormat.ARGB32);
            var sample = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            var previous = RenderTexture.active;
            try
            {
                RenderPipeline.SubmitRenderRequest(camera,
                    new UniversalRenderPipeline.SingleCameraRequest { destination = target });
                RenderTexture.active = target;
                sample.ReadPixels(new Rect(32, 32, 1, 1), 0, 0);
                var pixel = sample.GetPixel(0, 0);
                Assert.That(pixel.b, Is.GreaterThan(pixel.r + 0.1f), $"Fond bleu attendu, pixel lu {pixel}.");
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
                Object.Destroy(sample);
            }

            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void InputSystemSupportsKeyboardAndTouchDevices()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            try
            {
                var touchscreen = InputSystem.AddDevice<Touchscreen>();
                try
                {
                    Assert.That(keyboard.leftArrowKey, Is.Not.Null);
                    Assert.That(touchscreen.primaryTouch, Is.Not.Null);
                }
                finally
                {
                    InputSystem.RemoveDevice(touchscreen);
                }
            }
            finally
            {
                InputSystem.RemoveDevice(keyboard);
            }
        }
    }
}
