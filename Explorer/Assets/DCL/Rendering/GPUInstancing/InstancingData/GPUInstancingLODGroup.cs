using DCL.Diagnostics;
using DCL.Rendering.GPUInstancing.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DCL.Rendering.GPUInstancing.InstancingData
{
    [Serializable]
    public class GPUInstancingLODGroup : MonoBehaviour, IEquatable<GPUInstancingLODGroup>
    {
        private const int MAX_LODS_LEVEL = 8;
        public Shader[] whitelistedShaders;

        [Header("REFERENCES")]
        public string Name;
        public LODGroup Reference;
        public Transform Transform;
        public List<Renderer> RefRenderers;

        [Header("LOD GROUP DATA")]
        public float ObjectSize;
        public Bounds Bounds;
        public float[] LodsScreenSpaceSizes;
        public Matrix4x4 LODSizesMatrix;

        [Space]
        public List<CombinedLodsRenderer> CombinedLodsRenderers;

        [ContextMenu(nameof(HideAll))]
        public void HideAll()
        {
            foreach (Renderer refRenderer in RefRenderers)
                refRenderer.enabled = false;

            if (Reference == null) return;
            bool isAllRenderersDisabled = Reference.GetLODs().All(lod => lod.renderers.All(lodRenderer => lodRenderer.enabled));
            if (isAllRenderersDisabled) Reference.enabled = false;
        }

        [ContextMenu(nameof(ShowAll))]
        public void ShowAll()
        {
            if(Reference!= null) Reference.enabled = true;

            foreach (Renderer refRenderer in RefRenderers) refRenderer.enabled = true;
        }

        [ContextMenu(nameof(CollectStandaloneRenderers))]
        private void CollectStandaloneRenderers()
        {
            var renderer = GetComponent<MeshRenderer>();
            var meshFilter = GetComponent<MeshFilter>();

            CombinedLodsRenderers = new List<CombinedLodsRenderer> {
                new (renderer.sharedMaterial,  renderer,  meshFilter)
            };
            RefRenderers = new List<Renderer> { renderer };

            // Position at origin (but not scale!)
            transform.position = Vector3.zero;
            transform.rotation = Quaternion.identity;

            // LOD Group
            Reference = null;
            Transform = this.transform;
            Name = this.transform.name;

            LodsScreenSpaceSizes = new[] { 0.0f }; // Single LOD with maximum visibility
            Bounds = new Bounds();
            Bounds.Encapsulate(meshFilter.sharedMesh.bounds);

            ObjectSize = Mathf.Max(Bounds.size.x, Bounds.size.y, Bounds.size.z);

            BuildLODMatrix(1);
        }

        [ContextMenu(nameof(CollectSelfData))]
        private void CollectSelfData()
        {
            if (GetComponentsInChildren<GPUInstancingLODGroup>().Length > 1)
                ReportHub.LogWarning(ReportCategory.GPU_INSTANCING, $"{name} has nested GPU instancing candidates, that could lead to duplication of meshes!");

            CombinedLodsRenderers = new List<CombinedLodsRenderer>();

            LODGroup lodGroup = GetComponent<LODGroup>();
            if (lodGroup == null)
            {
                ReportHub.LogWarning(ReportCategory.GPU_INSTANCING, "Selected GameObject does not have a LODGroup component.");
                return;
            }

            LOD[] lods = lodGroup.GetLODs();
            if (lods.Length == 0)
            {
                ReportHub.LogWarning(ReportCategory.GPU_INSTANCING, "LODGroup has no LOD levels.");
                return;
            }

            // Position at origin (but not scale!)
            transform.position = Vector3.zero;
            transform.rotation = Quaternion.identity;

            var meshCombiner = new LodsMeshCombiner(whitelistedShaders, this.transform, lods);
            meshCombiner.CollectCombineInstances();

            if (meshCombiner.IsEmpty())
            {
                ReportHub.LogWarning(ReportCategory.GPU_INSTANCING, "No valid meshes found to combine.");
                return;
            }

            RefRenderers = meshCombiner.RefRenderers;
            CombinedLodsRenderers.AddRange(meshCombiner.BuildCombinedLodsRenderers());

            // LOD Group
            Reference = lodGroup;
            Transform = lodGroup.transform;
            Name = lodGroup.transform.name;

            lodGroup.RecalculateBounds();
            ObjectSize = lodGroup.size;

            LodsScreenSpaceSizes = new float[lods.Length];

            for (var i = 0; i < lods.Length && i < MAX_LODS_LEVEL; i++)
                LodsScreenSpaceSizes[i] = lods[i].screenRelativeTransitionHeight;

            BuildLODMatrix(lods.Length);
            UpdateBoundsByCombinedLods();

            HideAll();
        }

        private void BuildLODMatrix(int lodsLength)
        {
            const float OVERLAP_FACTOR = 0.20f;

            LODSizesMatrix = new Matrix4x4();

            var rowEnd = 0;
            var col = 0;

            for (var i = 0; i < lodsLength && i < MAX_LODS_LEVEL; i++)
            {
                float endValue = LodsScreenSpaceSizes[i];
                float startValue;

                if (i == 0)
                    startValue = 1f;
                else
                {
                    float prevEnd = LodsScreenSpaceSizes[i - 1];
                    float difference = prevEnd - endValue;
                    float overlap = difference * OVERLAP_FACTOR;

                    startValue = prevEnd + overlap;
                }

                // Write [startValue, endValue] into LODSizesMatrix.
                //    The pattern:
                //      - row0 & row1 for 'start'
                //      - row2 & row3 for 'end'
                //    i < 4 => row0 & row2, i >= 4 => row1 & row3
                int rowStart = i < 4 ? 0 : 1;
                rowEnd = rowStart + 2; // 2 or 3
                col = i % 4;

                LODSizesMatrix[rowStart, col] = startValue;
                LODSizesMatrix[rowEnd, col] = endValue;
            }

            LODSizesMatrix[rowEnd, col] = 0; // zero for the end of last LOD
        }
        private void UpdateBoundsByCombinedLods()
        {
            Bounds = CombinedLodsRenderers[0].CombinedMesh.bounds;
            for (var i = 1; i < CombinedLodsRenderers.Count; i++)
                Bounds.Encapsulate(CombinedLodsRenderers[i].CombinedMesh.bounds);
        }

        public bool Equals(GPUInstancingLODGroup other)
        {
            const float EPS = 0.001f;

            if (other == null) return false;
            if (ReferenceEquals(this, other)) return true;

            // Check basic properties
            if (Name != other.Name) return false;
            if (Math.Abs(ObjectSize - other.ObjectSize) > EPS) return false;

            // Check LOD sizes
            if (LodsScreenSpaceSizes == null || other.LodsScreenSpaceSizes == null || LodsScreenSpaceSizes.Length != other.LodsScreenSpaceSizes.Length)
                return false;

            for (var i = 0; i < LodsScreenSpaceSizes.Length; i++)
                if (Math.Abs(LodsScreenSpaceSizes[i] - other.LodsScreenSpaceSizes[i]) > EPS)
                    return false;

            // Check CombinedLods
            if (CombinedLodsRenderers == null || other.CombinedLodsRenderers == null || CombinedLodsRenderers.Count != other.CombinedLodsRenderers.Count)
                return false;

            for (var i = 0; i < CombinedLodsRenderers.Count; i++)
            {
                var thisRenderer = CombinedLodsRenderers[i];
                var otherRenderer = other.CombinedLodsRenderers[i];

                if (thisRenderer.CombinedMesh.vertexCount != otherRenderer.CombinedMesh.vertexCount ||
                    thisRenderer.CombinedMesh.subMeshCount != otherRenderer.CombinedMesh.subMeshCount ||
                    thisRenderer.SharedMaterial != otherRenderer.SharedMaterial)
                    return false;
            }

            return true;
        }

        public override int GetHashCode()
        {
            var hashCode = new HashCode();
            hashCode.Add(Name);
            hashCode.Add(ObjectSize);

            // Add hash of LOD sizes
            if (LodsScreenSpaceSizes != null)
            {
                foreach (float size in LodsScreenSpaceSizes)
                    hashCode.Add(size);
            }

            return hashCode.ToHashCode();
        }

        public override bool Equals(object obj) =>
            Equals(obj as GPUInstancingLODGroup);

        public static bool operator ==(GPUInstancingLODGroup left, GPUInstancingLODGroup right)
        {
            if (ReferenceEquals(left, right))
                return true;

            return left is not null && left.Equals(right);
        }

        public static bool operator !=(GPUInstancingLODGroup left, GPUInstancingLODGroup right) =>
            !(left == right);
    }
}
