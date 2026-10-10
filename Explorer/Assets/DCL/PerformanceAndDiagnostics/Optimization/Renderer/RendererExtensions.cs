using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace DCL.PerformanceAndDiagnostics.Optimization.Renderer
{
    public static class RendererExtensions
    {
        private static readonly int CULL_PROPERTY = Shader.PropertyToID("_Cull");

        public static void ForceBackfaceCulling(this UnityEngine.Renderer renderer)
        {
            List<Material> sharedMaterials = ListPool<Material>.Get();

            try
            {
                renderer.GetSharedMaterials(sharedMaterials);

                foreach (Material material in sharedMaterials)
                {
                    if (material == null || !material.HasProperty(CULL_PROPERTY))
                        continue;

                    material.SetInt(CULL_PROPERTY, (int)CullMode.Back);
                }
            }
            finally
            {
                ListPool<Material>.Release(sharedMaterials);
            }
        }
    }
}
