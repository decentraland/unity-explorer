using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Utility;

namespace DCL.Roads.GPUInstancing.Playground
{
    [Serializable]
    public class GPUInstancingLodLevel_Old
    {
        public MeshRenderingData_Old[] MeshRenderingDatas;
    }

    [Serializable]
    public class MeshRenderingData_Old
    {
        private const string GPU_INSTANCING_KEYWORD = "_GPU_INSTANCER_BATCHER";

        public Mesh SharedMesh;
        public MeshRenderer Renderer;

        public RenderParams[] RenderParamsArray { get; private set; }// array for submeshes

        public MeshRenderingData_Old(MeshRenderer renderer)
        {
            SharedMesh = renderer.GetComponent<MeshFilter>().sharedMesh;
            Renderer = renderer;
        }

        public void Initialize(Dictionary<Material, Material> instancingMaterials)
        {
            RenderParamsArray = CreateRenderParams(instancingMaterials);
        }

        // RenderParams are not Serializable, that is why we save collected raw data and transition to RenderParams at runtime
        private RenderParams[] CreateRenderParams(Dictionary<Material, Material> instancingMaterials)
        {
            var sharedMaterials = Renderer.sharedMaterials;
            var renderParamsArray = new RenderParams[sharedMaterials.Length];

            for (var i = 0; i < sharedMaterials.Length; i++)
            {
                var sharedMat = sharedMaterials[i];
                if (!instancingMaterials.TryGetValue(sharedMat, out Material instancedMat))
                {
                    instancedMat = new Material(sharedMat) { name = $"{sharedMat.name}_GPUInstancingIndirect" };
                    instancedMat.EnableKeyword(new LocalKeyword(instancedMat.shader, GPU_INSTANCING_KEYWORD));
                    instancingMaterials.Add(sharedMat, instancedMat);
                }

                renderParamsArray[i] = new RenderParams
                {
                    material = instancedMat,
                    layer = Renderer.gameObject.layer,
                    lightProbeProxyVolume = null, // no custom proxy volume
                    lightProbeUsage = Renderer.lightProbeUsage,
                    motionVectorMode = Renderer.motionVectorGenerationMode,
                    receiveShadows = Renderer.receiveShadows,
                    reflectionProbeUsage = Renderer.reflectionProbeUsage,
                    rendererPriority = Renderer.rendererPriority,
                    renderingLayerMask = Renderer.renderingLayerMask,
                    shadowCastingMode = Renderer.shadowCastingMode,
                };
            }

            return renderParamsArray;
        }
    }
}
