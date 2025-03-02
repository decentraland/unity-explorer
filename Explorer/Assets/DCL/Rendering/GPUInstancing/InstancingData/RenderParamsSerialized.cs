using DCL.Diagnostics;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace DCL.Rendering.GPUInstancing.InstancingData
{
    [Serializable]
    public struct RenderParamsSerialized
    {
        private const string GPU_INSTANCING_KEYWORD = "_GPU_INSTANCER_BATCHER";

        public Renderer RefRenderer;

        // Layers
        public int layer;
        public uint renderingLayerMask;
        public int rendererPriority;

        // Shadows
        public bool receiveShadows;
        public ShadowCastingMode shadowCastingMode;

        // Probes
        public ReflectionProbeUsage reflectionProbeUsage;
        public LightProbeUsage lightProbeUsage;

        public MotionVectorGenerationMode motionVectorMode;

        public RenderParamsSerialized(Renderer rend)
        {
            RefRenderer = rend;

            layer = rend.gameObject.layer;
            renderingLayerMask = rend.renderingLayerMask;
            rendererPriority = rend.rendererPriority;

            receiveShadows = rend.receiveShadows;
            shadowCastingMode = rend.shadowCastingMode;

            reflectionProbeUsage = rend.reflectionProbeUsage;
            lightProbeUsage = rend.lightProbeUsage;

            motionVectorMode = rend.motionVectorGenerationMode;
        }

        public RenderParams ToRenderParams(Material sharedMat, Dictionary<Material, Material> instancingMaterials)
        {
            if (!instancingMaterials.TryGetValue(sharedMat, out Material instancedMat))
            {
                ReportHub.Log(ReportCategory.GPU_INSTANCING, $"Creating new GPU Instanced sharedMaterial based on material: {sharedMat.name}");

                var keyword = new LocalKeyword(sharedMat.shader, GPU_INSTANCING_KEYWORD);
                sharedMat.DisableKeyword(keyword);

                instancedMat = new Material(sharedMat) { name = $"{sharedMat.name}_GPUInstancingIndirect" };
                instancedMat.EnableKeyword(keyword);
                instancingMaterials.Add(sharedMat, instancedMat);
            }

            return new RenderParams
            {
                material = instancedMat,

                layer = layer,
                renderingLayerMask = renderingLayerMask,
                rendererPriority = rendererPriority,
                receiveShadows = receiveShadows,
                shadowCastingMode = shadowCastingMode,
                reflectionProbeUsage = reflectionProbeUsage,
                lightProbeUsage = lightProbeUsage,
                motionVectorMode = motionVectorMode,
            };
        }
    }
}
