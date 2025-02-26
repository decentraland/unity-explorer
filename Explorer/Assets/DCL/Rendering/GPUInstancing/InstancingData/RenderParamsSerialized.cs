using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace DCL.Roads.GPUInstancing.Playground
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
        // public LightProbeProxyVolume lightProbeProxyVolume;

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
            // lightProbeProxyVolume = null; // no custom proxy volume

            motionVectorMode = rend.motionVectorGenerationMode;
        }

        public RenderParams ToRenderParams(Material sharedMat, Dictionary<Material, Material> instancingMaterials)
        {
            if (!instancingMaterials.TryGetValue(sharedMat, out Material instancedMat))
            {
                instancedMat = new Material(sharedMat) { name = $"{sharedMat.name}_GPUInstancingIndirect" };
                instancedMat.EnableKeyword(new LocalKeyword(instancedMat.shader, GPU_INSTANCING_KEYWORD));
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
                // lightProbeProxyVolume = lightProbeProxyVolume, // no custom proxy volume
                motionVectorMode = motionVectorMode,
            };
        }
    }
}
