using System;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.Roads.GPUInstancing.Playground
{
    [Serializable]
    public class GPUInstancingLodLevel
    {
        public MeshRenderingData[] MeshRenderingDatas;
    }

    [Serializable]
    public class GPUInstancingCandidate
    {
        private const int MAX_LODS_LEVEL = 8;
        public LODGroup Reference;

        public List<PerInstanceBuffer> InstancesBuffer;

        public float ObjectSize;
        public Bounds Bounds;

        public float[] LodsScreenSpaceSizes;
        public List<GPUInstancingLodLevel> Lods;

        public GPUInstancingCandidate(LODGroup lodGroup, Matrix4x4 localToRootMatrix)
        {
            Reference = lodGroup;

            UnityEngine.LOD[] lodLevels = lodGroup.GetLODs();
            lodGroup.RecalculateBounds();

            InstancesBuffer = new List<PerInstanceBuffer> { new () { instMatrix = localToRootMatrix } };

            ObjectSize = lodGroup.size;

            LodsScreenSpaceSizes = new float [lodLevels.Length];
            Lods = new List<GPUInstancingLodLevel>();

            for (var i = 0; i < lodLevels.Length && i < MAX_LODS_LEVEL; i++)
            {
                UnityEngine.LOD lod = lodLevels[i];

                LodsScreenSpaceSizes[i] = lod.screenRelativeTransitionHeight;

                var lodMeshes = new List<MeshRenderingData>();
                foreach (var renderer in lod.renderers)
                {
                    if (renderer is MeshRenderer meshRenderer && renderer.sharedMaterial != null)
                        lodMeshes.Add(new MeshRenderingData(meshRenderer));
                }

                if(lodMeshes.Count > 0)
                    Lods.Add(new GPUInstancingLodLevel{ MeshRenderingDatas = lodMeshes.ToArray()});
            }

            UpdateBounds();
        }

        public GPUInstancingCandidate(MeshRenderer meshRenderer, Matrix4x4 localToRootMatrix)
        {
            if (meshRenderer.sharedMaterial == null) return;

            Reference = null;  // No LODGroup
            InstancesBuffer = new List<PerInstanceBuffer> { new() { instMatrix = localToRootMatrix } };

            if (meshRenderer.TryGetComponent(out MeshFilter mf))
                ObjectSize = mf.sharedMesh.bounds.extents.magnitude * 2f;
            else
                ObjectSize = 1f;

            LodsScreenSpaceSizes = new[] { 1.0f }; // Single LOD => We only have 1 screen space size and 1 LOD level
            Lods = new List<GPUInstancingLodLevel>(1); // only 1 lod level

            var singleLodMeshes = new List<MeshRenderingData> { new (meshRenderer) };
            Lods.Add(new GPUInstancingLodLevel { MeshRenderingDatas = singleLodMeshes.ToArray() });

            UpdateBounds();
        }


        // TODO (Vit): calculate bounds properly
        public void UpdateBounds()
        {
            var isInitialized = false;

            foreach (GPUInstancingLodLevel lodLevel in Lods)
            foreach (MeshRenderingData data in lodLevel.MeshRenderingDatas)
            {
                if (!isInitialized)
                {
                    Bounds = data.SharedMesh.bounds;
                    isInitialized = true;
                }
                else Bounds.Encapsulate(data.SharedMesh.bounds);
            }
        }
    }
}
