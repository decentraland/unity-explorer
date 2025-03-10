using DCL.Diagnostics;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace DCL.Rendering.GPUInstancing.InstancingData
{
    public class GPUInstancingMaterialsCache : IDisposable
    {
        private const string GPU_INSTANCING_KEYWORD = "_GPU_INSTANCER_BATCHER";

        private readonly Dictionary<Material, Material> instancingMaterials = new ();

        public Material GetInstancedMaterial(Material sharedMat)
        {
            if (!instancingMaterials.TryGetValue(sharedMat, out Material instancedMat))
            {
                ReportHub.Log(ReportCategory.GPU_INSTANCING, $"Creating new GPU Instanced sharedMaterial based on material: {sharedMat.name}");

                var keyword = new LocalKeyword(sharedMat.shader, GPU_INSTANCING_KEYWORD);
                instancedMat = new Material(sharedMat) { name = $"{sharedMat.name}_GPUInstancingIndirect" };

#if UNITY_EDITOR
                if (sharedMat.parent != null)
                {
                    sharedMat.parent.EnableKeyword(keyword);

                    instancedMat = new Material(sharedMat.parent.shader);
                    instancedMat.CopyPropertiesFromMaterial(sharedMat);
                }
#endif

                sharedMat.DisableKeyword(keyword);
                instancedMat.EnableKeyword(keyword);

                instancingMaterials.Add(sharedMat, instancedMat);
            }

            return instancedMat;
        }

        public void Dispose()
        {
            instancingMaterials.Clear();
        }
    }
}
