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

        public RenderParams ToRenderParams(Material sharedMat, GPUInstancingMaterialsCache materialsCache) =>
            new()
            {
                material = materialsCache.GetInstancedMaterial(sharedMat),

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
