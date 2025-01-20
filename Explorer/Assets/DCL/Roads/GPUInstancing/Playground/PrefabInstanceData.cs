using DCL.Roads.Playground;
using System;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace DCL.Roads.GPUInstancing.Playground
{
    [Serializable]
    public class PrefabInstanceData
    {
        public MeshData[] Meshes;
        public LODGroupData[] LODGroups;
    }

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
    public class MeshData
    {
        public MeshRenderer Renderer;
        public Transform Transform;

        public Mesh SharedMesh;

        public bool ReceiveShadows;
        public ShadowCastingMode ShadowCastingMode;

        public Material[] SharedMaterials;

        public GPUInstancedRenderer ToGPUInstancedRenderer() =>
            new (SharedMesh, SharedMaterials.Select(mat => new RenderParams(mat)
            {
                receiveShadows = ReceiveShadows,
                shadowCastingMode = ShadowCastingMode,
            }).ToArray());
    }
}
