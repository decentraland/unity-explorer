using System.Collections.Generic;
using UnityEngine;

namespace DCL.Roads.GPUInstancing.Playground
{
    public class GPUInstancingPrefabData : MonoBehaviour
    {
        public List<GPUInstancingLODGroupWithBuffer> IndirectCandidates;
        private Dictionary<GPUInstancingLODGroup, List<PerInstanceBuffer>> candidatesTable;

        [ContextMenu(nameof(CollectSelfData))]
        public void CollectSelfData()
        {
            IndirectCandidates = new List<GPUInstancingLODGroupWithBuffer>();
            candidatesTable = new Dictionary<GPUInstancingLODGroup, List<PerInstanceBuffer>>();

            foreach (GPUInstancingLODGroup lodGroup in GetComponentsInChildren<GPUInstancingLODGroup>())
            {
                AdjustMaterialChangeInPrefab(lodGroup);

                Matrix4x4 localToRootMatrix = transform.worldToLocalMatrix * lodGroup.transform.localToWorldMatrix; // root * child
                TryAddToCollected(lodGroup, localToRootMatrix);
            }

            foreach (KeyValuePair<GPUInstancingLODGroup,List<PerInstanceBuffer>> pair in candidatesTable)
                IndirectCandidates.Add(new GPUInstancingLODGroupWithBuffer(pair.Key, pair.Value));
        }

        private static void AdjustMaterialChangeInPrefab(GPUInstancingLODGroup lodGroup)
        {
            foreach (var combinedRenderer in lodGroup.CombinedLodsRenderers)
                combinedRenderer.SharedMaterial = combinedRenderer.RenderParamsSerialized[0].RefRenderer.sharedMaterials[combinedRenderer.SubMeshId];
        }

        private void TryAddToCollected(GPUInstancingLODGroup newCandidate, Matrix4x4 localToRootMatrix)
        {
            if (candidatesTable.TryGetValue(newCandidate, out List<PerInstanceBuffer> instances))
            {
                Debug.Log($"Same LODGroup for {newCandidate.Transform.name}", newCandidate);
                instances.Add(new PerInstanceBuffer(localToRootMatrix));
            }
            else
                candidatesTable.Add(newCandidate, new List<PerInstanceBuffer> { new (localToRootMatrix) });
        }
    }
}
