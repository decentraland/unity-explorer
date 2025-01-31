using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DCL.Roads.GPUInstancing.Playground
{
    [Serializable]
    public class GPUInstancingLodLevel
    {
        public MeshRenderingData[] MeshRenderingDatas;
    }

    [Serializable]
    public class GPUInstancingCandidate : IEquatable<GPUInstancingCandidate>
    {
        private const int MAX_LODS_LEVEL = 8;

        public string Name;
        public LODGroup Reference;
        public Transform Transform;

        public List<PerInstanceBuffer> InstancesBuffer;

        public float ObjectSize;
        public Bounds Bounds;

        public float[] LodsScreenSpaceSizes;
        public List<GPUInstancingLodLevel> Lods;

        public GPUInstancingCandidate(GPUInstancingCandidate candidate)
        {
            InstancesBuffer = candidate.InstancesBuffer;
            ObjectSize = candidate.ObjectSize;
            Bounds = candidate.Bounds;

            LodsScreenSpaceSizes = candidate.LodsScreenSpaceSizes;
            Lods = candidate.Lods;
        }

        public GPUInstancingCandidate(GPUInstancingCandidate candidate, HashSet<PerInstanceBuffer> instanceBuffers)
        {
            Name = candidate.Name;
            InstancesBuffer = instanceBuffers.ToList();
            ObjectSize = candidate.ObjectSize;
            Bounds = candidate.Bounds;

            LodsScreenSpaceSizes = candidate.LodsScreenSpaceSizes;
            Lods = candidate.Lods;
        }

        public GPUInstancingCandidate(LODGroup lodGroup, Matrix4x4 localToRootMatrix)
        {
            Reference = lodGroup;
            Transform = lodGroup.transform;
            Name = lodGroup.transform.name;

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

                foreach (Renderer renderer in lod.renderers)
                {
                    if (renderer is MeshRenderer meshRenderer && renderer.sharedMaterial != null)
                        lodMeshes.Add(new MeshRenderingData(meshRenderer));
                }

                if (lodMeshes.Count > 0)
                    Lods.Add(new GPUInstancingLodLevel { MeshRenderingDatas = lodMeshes.ToArray() });
            }

            UpdateBounds();
        }

        public GPUInstancingCandidate(MeshRenderer meshRenderer, Matrix4x4 localToRootMatrix)
        {
            if (meshRenderer.sharedMaterial == null) return;

            Reference = null; // No LODGroup
            Transform = meshRenderer.transform;
            Name = Transform.name;

            InstancesBuffer = new List<PerInstanceBuffer> { new () { instMatrix = localToRootMatrix } };

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

        /// <summary>
        ///     Returns true if the rendering data (all LOD levels and, for each level, each MeshRenderingData's SharedMesh and SharedMaterial) is the same between this candidate and the other.
        /// </summary>
        public bool Equals(GPUInstancingCandidate other)
        {
            if (other == null)
                return false;

            if (ReferenceEquals(this, other))
                return true;

            if (Lods.Count != other.Lods.Count)
                return false;


            for (var i = 0; i < Lods.Count; i++)
            {
                // if (Lods[i].MeshRenderingDatas == null || other.Lods[i].MeshRenderingDatas == null)
                // {
                //     if (Lods[i].MeshRenderingDatas != other.Lods[i].MeshRenderingDatas)
                //         return false;
                // }
                // else
                if (!Lods[i].MeshRenderingDatas.SequenceEqual(other.Lods[i].MeshRenderingDatas))
                    return false;
            }

            // for (var i = 0; i < Lods.Count; i++)
            // {
            //     var myData = Lods[i].MeshRenderingDatas;
            //     var otherData = other.Lods[i].MeshRenderingDatas;
            //
            //     if (myData.Length != otherData.Length)
            //         return false;
            //
            //     for (var j = 0; j < myData.Length; j++)
            //         if (!myData[j].Equals(otherData[j]))
            //             return false;
            // }

            return true;
        }

        public override bool Equals(object obj) =>
            Equals(obj as GPUInstancingCandidate);

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = 17;

                if (Lods != null)
                    hash = Lods.Where(lod => lod.MeshRenderingDatas != null)
                               .SelectMany(lod => lod.MeshRenderingDatas)
                               .Aggregate(hash, (current, data) => (current * 23) + (data?.GetHashCode() ?? 0));

                return hash;
            }
        }

        public static bool operator ==(GPUInstancingCandidate left, GPUInstancingCandidate right)
        {
            if (ReferenceEquals(left, right))
                return true;

            return left is not null && left.Equals(right);
        }

        public static bool operator !=(GPUInstancingCandidate left, GPUInstancingCandidate right) =>
            !(left == right);
    }
}
