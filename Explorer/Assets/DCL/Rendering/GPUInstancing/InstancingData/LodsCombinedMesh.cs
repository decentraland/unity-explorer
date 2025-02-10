using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace DCL.Roads.GPUInstancing.Playground
{
    [Serializable]
    public class LodsCombinedMesh
    {
        public List<CombineInstance> СombineInstances;

        public Transform parent;

        public Mesh LodCombinedMesh;
        public Material SharedMaterial;

        public List<RenderParamsSerialized> RenderParamsSerialized;

        public LodsCombinedMesh(CombineInstance combineInstance, Material material, Renderer rend)
        {
            СombineInstances = new List<CombineInstance>();
            RenderParamsSerialized = new List<RenderParamsSerialized>();
            SharedMaterial = material;
            parent = rend.transform.parent;

            AddCombineInstance(combineInstance, rend);
        }

        public void AddCombineInstance(CombineInstance combineInstance, Renderer rend)
        {
            СombineInstances.Add(combineInstance);
            RenderParamsSerialized.Add(new RenderParamsSerialized(rend));
        }
    }

    [Serializable]
    public struct RenderParamsSerialized
    {
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
        public LightProbeProxyVolume lightProbeProxyVolume;

        public MotionVectorGenerationMode motionVectorMode;

        public RenderParamsSerialized(Renderer rend)
        {
            layer = rend.gameObject.layer;
            renderingLayerMask = rend.renderingLayerMask;
            rendererPriority = rend.rendererPriority;

            receiveShadows = rend.receiveShadows;
            shadowCastingMode = rend.shadowCastingMode;

            reflectionProbeUsage = rend.reflectionProbeUsage;
            lightProbeUsage = rend.lightProbeUsage;
            lightProbeProxyVolume = null; // no custom proxy volume

            motionVectorMode = rend.motionVectorGenerationMode;
        }
    }
}
