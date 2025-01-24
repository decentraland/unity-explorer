using DCL.Roads.Playground;
using System;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace DCL.Roads.GPUInstancing.Playground
{
    [Serializable]
    public class LODGroupData
    {
        public LODGroup LODGroup;
        public Transform Transform;

        public float ObjectSize;
        public Bounds LODBounds;

        [Space]
        public LODEntryMeshData[] LODs;

        public void UpdateGroupBounds()
        {
            var isInitialized = false;

            foreach (LODEntryMeshData mid in LODs)
            foreach (MeshData data in mid.Meshes)
            {
                if (!isInitialized)
                {
                    LODBounds = data.SharedMesh.bounds;
                    isInitialized = true;
                }
                else LODBounds.Encapsulate(data.SharedMesh.bounds);
            }
        }
    }

    [Serializable]
    public class LODEntryMeshData
    {
        public float ScreenRelativeTransitionHeight;
        public MeshData[] Meshes;
    }

    [Serializable]
    public class MeshData : IEquatable<MeshData>
    {
        public MeshRenderer Renderer;
        public Transform Transform;
        public Matrix4x4 LocalToRootMatrix;

        public Mesh SharedMesh;

        public bool ReceiveShadows;
        public ShadowCastingMode ShadowCastingMode;

        public Material[] SharedMaterials;

        public GPUInstancedRenderer ToGPUInstancedRenderer() =>
            new (SharedMesh, SharedMaterials.Select(mat => new RenderParams(mat)
            {
                receiveShadows = ReceiveShadows,
                shadowCastingMode = ShadowCastingMode,
                // ?? worldBounds = new Bounds(center: Vector3.zero, size: Vector3.one * 999999f), ?? what value ??
            }).ToArray());

        public bool Equals(MeshData other) =>
            other != null &&
            Equals(SharedMesh, other.SharedMesh) && // Mesh
            ReceiveShadows == other.ReceiveShadows && ShadowCastingMode == other.ShadowCastingMode && // Shadows
            SharedMaterials != null && other.SharedMaterials != null && SharedMaterials.SequenceEqual(other.SharedMaterials); // Materials

        public override bool Equals(object obj) =>
            obj is MeshData other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = 17;
                hash = (hash * 23) + (SharedMesh != null ? SharedMesh.GetHashCode() : 0);
                hash = (hash * 23) + ReceiveShadows.GetHashCode();
                hash = (hash * 23) + ShadowCastingMode.GetHashCode();

                if (SharedMaterials == null) return hash;

                foreach (var material in SharedMaterials)
                    hash = (hash * 23) + (material != null ? material.GetHashCode() : 0);

                return hash;
            }
        }


    }
}
