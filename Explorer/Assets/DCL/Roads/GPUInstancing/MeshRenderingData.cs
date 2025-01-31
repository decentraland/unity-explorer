using DCL.Roads.Playground;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using Utility;

namespace DCL.Roads.GPUInstancing.Playground
{
    [Serializable]
    public class MeshRenderingData : IEquatable<MeshRenderingData>
    {
        private const float STREET_MAX_HEIGHT = 10f;

        public Mesh SharedMesh;
        public MeshRenderer Renderer;

        public MeshRenderingData(MeshRenderer renderer)
        {
            SharedMesh = renderer.GetComponent<MeshFilter>().sharedMesh;
            Renderer = renderer;
        }

        // RenderParams are not Serializable, so that is why we save collected raw data and transition to RenderParams at runtime
        public GPUInstancedRenderer ToGPUInstancedRenderer(Dictionary<Material, Material> instancingMaterials)
        {
            var renderParams = Renderer.sharedMaterials.Select(sharedMat =>
            {
                if (!instancingMaterials.TryGetValue(sharedMat, out Material instancedMat))
                {
                    instancedMat = new Material(sharedMat) { name = $"{sharedMat.name}_Instanced" };
                    instancedMat.EnableKeyword(new LocalKeyword(instancedMat.shader, "_GPU_INSTANCER_BATCHER"));
                    sharedMat.DisableKeyword(new LocalKeyword(instancedMat.shader, "_GPU_INSTANCER_BATCHER"));
                    instancingMaterials.Add(sharedMat, instancedMat);
                }

                return new RenderParams
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
                    worldBounds = new Bounds(
                        center: Vector3.zero,
                        size: new Vector3(
                            GenesisCityData.EXTENTS.x * ParcelMathHelper.PARCEL_SIZE,
                            STREET_MAX_HEIGHT,
                            GenesisCityData.EXTENTS.y * ParcelMathHelper.PARCEL_SIZE
                        )
                    )
                };
            }).ToArray();

            var instancedRenderer = new GPUInstancedRenderer(SharedMesh, renderParams);
            return instancedRenderer;
        }

        // Equals when MeshFilter and MeshRenderer settings are same, but Transform could be different
        public bool Equals(MeshRenderingData other) =>
            other != null &&
            Equals(SharedMesh, other.SharedMesh) && // Mesh
            Renderer.receiveShadows == other.Renderer.receiveShadows && Renderer.shadowCastingMode == other.Renderer.shadowCastingMode && // Shadows
            Renderer.sharedMaterials != null && other.Renderer.sharedMaterials != null && Renderer.sharedMaterials.SequenceEqual(other.Renderer.sharedMaterials); // Materials

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
