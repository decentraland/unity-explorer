using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DCL.Roads.GPUInstancing
{
    [Serializable]
    public class LODGroupData // : IEquatable<LODInstanceData>
    {
        public Transform Transform;
        public float ObjectSize;
        public Bounds LODBounds;

        [Space]
        public LODEntryMeshData[] LODs;
    }

    [Serializable]
    public class LODEntryMeshData // : IEquatable<LODInstanceData>
    {
        public float Distance;
        public MeshData[] MeshData;
    }

    public class LODGroupDataBehaviour : MonoBehaviour
    {
        public LODGroupData LODGroupData;

        [ContextMenu(nameof(CollectSelfData))]
        private void CollectSelfData()
        {
            var lodGroup  = GetComponent<LODGroup>();
            lodGroup.RecalculateBounds();

            LODGroupData = new LODGroupData
            {
                Transform = transform,
                ObjectSize = lodGroup.size,
                LODs = CollectLODData(lodGroup.GetLODs()),
            };

            CalculateGroupBounds(LODGroupData);
        }

        private static LODEntryMeshData[] CollectLODData(UnityEngine.LOD[] lods)
        {
            return lods.Select(lod => new LODEntryMeshData
            {
                MeshData = CollectLODMeshData(lod).ToArray(),
                Distance = lod.screenRelativeTransitionHeight,
            }).ToArray();
        }

        private static List<MeshData> CollectLODMeshData(UnityEngine.LOD lod) =>
            (from renderer in lod.renderers
                let meshRenderer = renderer as MeshRenderer
                let meshFilter = renderer.GetComponent<MeshFilter>()
                where renderer != null && meshFilter != null
                select new MeshData
                {
                    Transform = renderer.transform,
                    Mesh = meshFilter.sharedMesh,
                    Materials = meshRenderer.sharedMaterials,
                }).ToList();

        private static void CalculateGroupBounds(LODGroupData lodGroup)
        {
            var isInitialized = false;

            foreach (LODEntryMeshData mid in lodGroup.LODs)
            foreach (var data in mid.MeshData)
            {
                if (!isInitialized)
                {
                    lodGroup.LODBounds = data.Mesh.bounds;
                    isInitialized = true;
                }
                else
                    lodGroup.LODBounds.Encapsulate(data.Mesh.bounds);
            }
        }

        private void CollectInstanceMatrices(List<Transform> instances, LODInstanceData instanceData)
        {
            foreach (var instance in instances)
                instanceData.Matrices.Add(instance.localToWorldMatrix);
        }
    }
}
