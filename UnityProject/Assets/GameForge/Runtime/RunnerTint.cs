using UnityEngine;

namespace GameForge.Runtime
{
    internal static class RunnerTint
    {
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorProperty = Shader.PropertyToID("_Color");

        public static void Apply(GameObject root, Color color)
        {
            var properties = new MaterialPropertyBlock();
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                var material = renderer.sharedMaterial;
                if (material == null)
                    continue;
                var property = material.HasProperty(BaseColor) ? BaseColor : ColorProperty;
                if (!material.HasProperty(property))
                    continue;
                renderer.GetPropertyBlock(properties);
                properties.SetColor(property, color);
                renderer.SetPropertyBlock(properties);
            }
        }
    }
}
