using DCL.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DCL.Rendering.GPUInstancing.InstancingData
{
    public class GPUInstancingPrefabData : MonoBehaviour
    {
        public List<GPUInstancingLODGroupWithBuffer> IndirectCandidates;
        private Dictionary<CombinedLODGroupData, List<PerInstanceBuffer>> candidatesTable;

        [ContextMenu(nameof(CollectSelfData))]
        public void CollectSelfData()
        {
            IndirectCandidates = new List<GPUInstancingLODGroupWithBuffer>();
            candidatesTable = new Dictionary<CombinedLODGroupData, List<PerInstanceBuffer>>();

            foreach (CombinedLODGroupData lodGroup in GetComponentsInChildren<CombinedLODGroupData>())
            {
                AdjustMaterialChangeInPrefab(lodGroup);

                Matrix4x4 localToRootMatrix = transform.worldToLocalMatrix * lodGroup.transform.localToWorldMatrix; // root * child
                TryAddToCollected(lodGroup, localToRootMatrix);
            }

            foreach (KeyValuePair<CombinedLODGroupData, List<PerInstanceBuffer>> pair in candidatesTable)
                IndirectCandidates.Add(new GPUInstancingLODGroupWithBuffer(pair.Key, pair.Value));
        }

        private static void AdjustMaterialChangeInPrefab(CombinedLODGroupData combinedLODGroupData)
        {
            foreach (CombinedLodsRenderer combinedRenderer in combinedLODGroupData.CombinedLodsRenderers)
                combinedRenderer.SharedMaterial = combinedRenderer.RenderParamsSerialized.RefRenderer.sharedMaterials[combinedRenderer.SubMeshId];
        }

        private void TryAddToCollected(CombinedLODGroupData newCandidate, Matrix4x4 localToRootMatrix)
        {
            Material firstCandidateMaterial = newCandidate.CombinedLodsRenderers.First().SharedMaterial;

            if (candidatesTable.TryGetValue(newCandidate, out List<PerInstanceBuffer> instances))
            {
                ReportHub.Log(ReportCategory.GPU_INSTANCING, $"Adding {nameof(PerInstanceBuffer)} to existing LODGroup: {newCandidate.Name}");
                instances.Add(new PerInstanceBuffer(localToRootMatrix, firstCandidateMaterial.mainTextureScale, firstCandidateMaterial.mainTextureOffset));
            }
            else
                candidatesTable.Add(newCandidate, new List<PerInstanceBuffer> { new (localToRootMatrix, firstCandidateMaterial.mainTextureScale, firstCandidateMaterial.mainTextureOffset) });
        }
    }
}
