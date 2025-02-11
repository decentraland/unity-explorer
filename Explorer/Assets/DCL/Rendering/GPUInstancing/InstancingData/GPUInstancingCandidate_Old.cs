using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DCL.Roads.GPUInstancing.Playground
{
    public class GPUInstancingCandidate_Old : IEquatable<GPUInstancingCandidate_Old>
    {
        private const int MAX_LODS_LEVEL = 8;
        public Shader[] whitelistedShaders;

        [Header("REFERENCES")]
        public string Name;
        public LODGroup Reference;
        public Transform Transform;

        [Header("BUFFERS")]
        public List<PerInstanceBuffer> InstancesBuffer;
        public List<Matrix4x4> InstancesBufferDirect;

        [Header("LOD GROUP DATA")]
        public float ObjectSize;
        public Bounds Bounds;
        public float[] LodsScreenSpaceSizes;

        [Space]
        public List<CombinedLodsRenderer> CombinedLodsRenderers;
        public List<GPUInstancingLodLevel_Old> Lods_Old;

         public GPUInstancingCandidate_Old(GPUInstancingCandidate_Old lodGroup)
        {
            InstancesBuffer = lodGroup.InstancesBuffer;

            ObjectSize = lodGroup.ObjectSize;
            Bounds = lodGroup.Bounds;

            LodsScreenSpaceSizes = lodGroup.LodsScreenSpaceSizes;
            Lods_Old = lodGroup.Lods_Old;
        }

        public GPUInstancingCandidate_Old(GPUInstancingCandidate_Old lodGroup, HashSet<PerInstanceBuffer> instanceBuffers)
        {
            Name = lodGroup.Name;
            InstancesBuffer = instanceBuffers.ToList();

            ObjectSize = lodGroup.ObjectSize;
            Bounds = lodGroup.Bounds;

            LodsScreenSpaceSizes = lodGroup.LodsScreenSpaceSizes;
            Lods_Old = lodGroup.Lods_Old;
        }

        public GPUInstancingCandidate_Old(LODGroup lodGroup, Matrix4x4 localToRootMatrix, Shader[] whitelistShaders)
        {
            this.whitelistedShaders = whitelistShaders;

            Reference = lodGroup;
            Transform = lodGroup.transform;
            Name = lodGroup.transform.name;

            LOD[] lodLevels = lodGroup.GetLODs();
            lodGroup.RecalculateBounds();

            InstancesBuffer = new List<PerInstanceBuffer> { new () { instMatrix = localToRootMatrix } };

            ObjectSize = lodGroup.size;

            LodsScreenSpaceSizes = new float [lodLevels.Length];
            Lods_Old = new List<GPUInstancingLodLevel_Old>();

            for (var i = 0; i < lodLevels.Length && i < MAX_LODS_LEVEL; i++)
            {
                LOD lod = lodLevels[i];

                LodsScreenSpaceSizes[i] = lod.screenRelativeTransitionHeight;

                var lodMeshes = new List<MeshRenderingData_Old>();

                foreach (Renderer renderer in lod.renderers)
                {
                    if (renderer is MeshRenderer meshRenderer && renderer.sharedMaterial != null && IsValidShader(meshRenderer.sharedMaterials, whitelistedShaders))
                        lodMeshes.Add(new MeshRenderingData_Old(meshRenderer));
                }

                if (lodMeshes.Count > 0)
                    Lods_Old.Add(new GPUInstancingLodLevel_Old { MeshRenderingDatas = lodMeshes.ToArray() });
            }

            UpdateBounds();
        }

        public GPUInstancingCandidate_Old(MeshRenderer meshRenderer, Matrix4x4 localToRootMatrix, Shader[] whitelistShaders)
        {
            this.whitelistedShaders = whitelistShaders;

            if (meshRenderer.sharedMaterial == null||!IsValidShader(meshRenderer.sharedMaterials, whitelistedShaders))
                return;

            Reference = null; // No LODGroup
            Transform = meshRenderer.transform;
            Name = Transform.name;

            InstancesBuffer = new List<PerInstanceBuffer> { new () { instMatrix = localToRootMatrix } };

            if (meshRenderer.TryGetComponent(out MeshFilter mf))
                ObjectSize = mf.sharedMesh.bounds.extents.magnitude * 2f;
            else
                ObjectSize = 1f;

            LodsScreenSpaceSizes = new[] { 1.0f }; // Single LOD => We only have 1 screen space size and 1 LOD level
            Lods_Old = new List<GPUInstancingLodLevel_Old>(1); // only 1 lod level

            var singleLodMeshes = new List<MeshRenderingData_Old> { new (meshRenderer) };
            Lods_Old.Add(new GPUInstancingLodLevel_Old { MeshRenderingDatas = singleLodMeshes.ToArray() });

            UpdateBounds();
        }

        public static bool IsValidShader(Material[] materials, Shader[] whitelistShaders)
        {
            if (materials == null || materials.Length == 0) return false;

            foreach (Material m in materials)
            {
                if (m == null)
                    return false;

                if (!IsInWhitelist(m, whitelistShaders))
                {
                    Debug.LogWarning($"Material {m.name} uses non-whitelisted shader: {m.shader.name}");
                    return false;
                }
            }

            return true;
        }

        private static bool IsInWhitelist(Material material, Shader[] whitelistShaders)
        {
            if (whitelistShaders == null || whitelistShaders.Length == 0)
            {
                Debug.LogError("No whitelist shaders defined!");
                return false;
            }

            return whitelistShaders.Where(shader => shader != null).
                                      Any(shader => material.shader == shader || material.shader.name == shader.name || material.shader.name.StartsWith(shader.name) || shader.name.StartsWith(material.shader.name));
        }

        public void PopulateDirectInstancingBuffer()
        {
            InstancesBufferDirect = new List<Matrix4x4>(InstancesBuffer.Count);

            for (var i = 0; i < InstancesBuffer.Count; i++)
                InstancesBufferDirect.Add(InstancesBuffer[i].instMatrix);
        }

        // TODO (Vit): calculate bounds properly
        public void UpdateBounds()
        {
            var isInitialized = false;

            foreach (GPUInstancingLodLevel_Old lodLevel in Lods_Old)
            foreach (MeshRenderingData_Old data in lodLevel.MeshRenderingDatas)
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
        public bool Equals(GPUInstancingCandidate_Old other)
        {
            if (other == null)
                return false;

            if (ReferenceEquals(this, other))
                return true;

            if (Lods_Old.Count != other.Lods_Old.Count)
                return false;

            for (var i = 0; i < Lods_Old.Count; i++)
            {
                // if (Lods[i].MeshRenderingDatas == null || other.Lods[i].MeshRenderingDatas == null)
                // {
                //     if (Lods[i].MeshRenderingDatas != other.Lods[i].MeshRenderingDatas)
                //         return false;
                // }
                // else
                if (!Lods_Old[i].MeshRenderingDatas.SequenceEqual(other.Lods_Old[i].MeshRenderingDatas))
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
            Equals(obj as GPUInstancingLODGroup);

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = 17;

                if (Lods_Old != null)
                    hash = Lods_Old.Where(lod => lod.MeshRenderingDatas != null)
                               .SelectMany(lod => lod.MeshRenderingDatas)
                               .Aggregate(hash, (current, data) => (current * 23) + (data?.GetHashCode() ?? 0));

                return hash;
            }
        }

        public static bool operator ==(GPUInstancingCandidate_Old left, GPUInstancingCandidate_Old right)
        {
            if (ReferenceEquals(left, right))
                return true;

            return left is not null && left.Equals(right);
        }

        public static bool operator !=(GPUInstancingCandidate_Old left, GPUInstancingCandidate_Old right) =>
            !(left == right);
    }
}
