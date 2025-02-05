using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Utility;

namespace DCL.Roads.GPUInstancing.Playground
{
    [Serializable]
    public class MeshRenderingData
    {
        private const float STREET_MAX_HEIGHT = 10f;
        private const string GPU_INSTANCING_KEYWORD = "_GPU_INSTANCER_BATCHER";

        public Mesh SharedMesh;
        public MeshRenderer Renderer;

        public RenderParams[] RenderParamsArray { get; private set; }// array for submeshes

        public MeshRenderingData(MeshRenderer renderer)
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
                    worldBounds = new Bounds(Vector3.zero, new Vector3(GenesisCityData.EXTENTS.x * ParcelMathHelper.PARCEL_SIZE, STREET_MAX_HEIGHT, GenesisCityData.EXTENTS.y * ParcelMathHelper.PARCEL_SIZE)),
                };
            }

            return renderParamsArray;
        }

        // Equals when MeshFilter and MeshRenderer settings are same, but Transform could be different
        public bool Equals(MeshRenderingData other)
        {
            if (other == null) return false;
            if (!Equals(SharedMesh, other.SharedMesh)) return false;
            if (Renderer.receiveShadows != other.Renderer.receiveShadows) return false;
            if (Renderer.shadowCastingMode != other.Renderer.shadowCastingMode) return false;

            var materials1 = Renderer.sharedMaterials;
            var materials2 = other.Renderer.sharedMaterials;

            if (materials1 == null || materials2 == null) return false;
            if (materials1.Length != materials2.Length) return false;

            for (var i = 0; i < materials1.Length; i++)
                if (!Equals(materials1[i], materials2[i])) return false;

            return true;
        }

        public override bool Equals(object obj) =>
            obj is MeshRenderingData other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = 17;
                hash = (hash * 23) + (SharedMesh != null ? SharedMesh.GetHashCode() : 0);
                hash = (hash * 23) + Renderer.receiveShadows.GetHashCode();
                hash = (hash * 23) + Renderer.shadowCastingMode.GetHashCode();

                if (Renderer.sharedMaterials == null) return hash;

                foreach (var material in Renderer.sharedMaterials)
                    hash = (hash * 23) + (material != null ? material.GetHashCode() : 0);

                return hash;
            }
        }
    }
}
