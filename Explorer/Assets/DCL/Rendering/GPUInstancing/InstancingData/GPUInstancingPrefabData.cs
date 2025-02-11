using System.Collections.Generic;
using UnityEngine;

namespace DCL.Roads.GPUInstancing.Playground
{
    public class GPUInstancingPrefabData : MonoBehaviour
    {
        public List<GPUInstancingLODGroup> IndirectCandidates;

        [ContextMenu(nameof(CollectSelfData))]
        public void CollectSelfData()
        {
            IndirectCandidates = new List<GPUInstancingLODGroup>();
            foreach (GPUInstancingLODGroup lodGroup in GetComponentsInChildren<GPUInstancingLODGroup>())
            {
                Matrix4x4 localToRootMatrix = transform.worldToLocalMatrix * lodGroup.transform.localToWorldMatrix; // root * child

                if (!TryAddToCollected(lodGroup, localToRootMatrix))
                    AddNewCandidate(lodGroup, localToRootMatrix);
            }
        }

        private bool TryAddToCollected(GPUInstancingLODGroup newCandidate, Matrix4x4 localToRootMatrix)
        {
            foreach (GPUInstancingLODGroup collectedCandidates in IndirectCandidates)
            {
                if (collectedCandidates.Equals(newCandidate))
                {
                    Debug.Log($"Same LODGroup: {newCandidate.Reference.name} and {collectedCandidates.Reference.name}", newCandidate);
                    collectedCandidates.InstancesBuffer.Add(new PerInstanceBuffer(localToRootMatrix));
                    return true;
                }
            }

            return false;
        }

        private void AddNewCandidate(GPUInstancingLODGroup lodGroup, Matrix4x4 localToRootMatrix)
        {
            lodGroup.InstancesBuffer = new List<PerInstanceBuffer> { new (localToRootMatrix) };
            IndirectCandidates.Add(lodGroup);
        }
    }
}
