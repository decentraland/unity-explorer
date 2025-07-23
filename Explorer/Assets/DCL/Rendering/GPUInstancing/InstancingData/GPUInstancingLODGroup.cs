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
        public GPUInstancingSettings GPUInstancingSettings;

        [Header("REFERENCES")]
        public string Name;
        public LODGroup Reference;
        public Transform Transform;
        public List<Renderer> RefRenderers;

        [Space]
        public LODGroupData LODGroupData;

        [Space]
        public List<CombinedLodsRenderer> CombinedLodsRenderers;

        [ContextMenu(nameof(HideAll))]
        public void HideAll()
        {
            foreach (Renderer refRenderer in RefRenderers)
                refRenderer.enabled = false;

            if (Reference == null) return;

            bool isAllRenderersDisabled = Reference.GetLODs().All(lod => lod.renderers.All(lodRenderer => lodRenderer.enabled));

            if (isAllRenderersDisabled)
                Reference.enabled = false;
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
            MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
            var meshFilter = GetComponent<MeshFilter>();

            RefRenderers = new List<Renderer> { meshRenderer };

            // Position at origin (but not scale!)
            transform.position = Vector3.zero;
            transform.rotation = Quaternion.identity;

            // LOD Group
            Reference = null;
            Transform = this.transform;
            Name = this.transform.name;

            LODGroupData = new LODGroupData(meshFilter.sharedMesh.bounds);

            CombinedLodsRenderers = new List<CombinedLodsRenderer>
            {
                new (meshRenderer.sharedMaterial, meshRenderer, meshFilter),
            };
        }

        [ContextMenu(nameof(CollectSelfData))]
        private void CollectSelfData()
        {
            if (GetComponentsInChildren<GPUInstancingLODGroup>().Length > 1)
                ReportHub.LogWarning(ReportCategory.GPU_INSTANCING, $"{name} has nested GPU instancing candidates, that could lead to duplication of meshes!");

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

            CombinedLodsRenderers = new List<CombinedLodsRenderer>();

            // Position at origin (but not scale!)
            transform.position = Vector3.zero;
            transform.rotation = Quaternion.identity;

            var meshCombiner = new LodsMeshCombiner(GPUInstancingSettings.WhitelistedShaders, transform, lods);
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

            LODGroupData = new LODGroupData(lodGroup, lods);
            LODGroupData.UpdateBounds(CombinedLodsRenderers);

            HideAll();
        }

        public bool Equals(GPUInstancingLODGroup other)
        {
            if (other == null) return false;
            if (ReferenceEquals(this, other)) return true;
            if (Name != other.Name) return false;

            if (LODGroupData != other.LODGroupData) return false;

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
            hashCode.Add(LODGroupData.GetHashCode());
            return hashCode.ToHashCode();
        }

        public override bool Equals(object obj) =>
            Equals(obj as GPUInstancingLODGroup);

        public static bool operator ==(GPUInstancingLODGroup left, GPUInstancingLODGroup right)
        {
            if (ReferenceEquals(left, right)) return true;
            return left is not null && left.Equals(right);
        }

        public static bool operator !=(GPUInstancingLODGroup left, GPUInstancingLODGroup right) =>
            !(left == right);
    }
}
