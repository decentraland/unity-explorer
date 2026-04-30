using UnityEngine;
using UnityEngine.Rendering;

namespace DCL.PerformanceAndDiagnostics.Optimization.Renderer
{
    public static class RendererExtensions
    {
        private static readonly int CULL_PROPERTY = Shader.PropertyToID("_Cull");

        public static void ForceBackfaceCulling(this UnityEngine.Renderer renderer)
        {
            Material[] sharedMaterials = renderer.sharedMaterials;

            foreach (Material material in sharedMaterials)
            {
                if (material == null || !material.HasProperty(CULL_PROPERTY))
                    continue;

                material.SetInt(CULL_PROPERTY, (int)CullMode.Back);
            }
        }

    }
}
