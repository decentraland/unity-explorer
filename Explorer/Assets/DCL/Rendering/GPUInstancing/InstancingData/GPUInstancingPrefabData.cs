using System;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.Roads.GPUInstancing.Playground
{
    [Serializable]
    public class GPUInstancingLODGroupWithBuffer : IEquatable<GPUInstancingLODGroupWithBuffer>
    {
        public string Name;
        public GPUInstancingLODGroup LODGroup;
        public List<PerInstanceBuffer> InstancesBuffer;

        public GPUInstancingLODGroupWithBuffer(GPUInstancingLODGroup lodGroup, List<PerInstanceBuffer> instances)
        {
            Name = lodGroup.Name;
            LODGroup = lodGroup;
            InstancesBuffer = instances;
        }

        public bool Equals(GPUInstancingLODGroupWithBuffer other)
        {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return true;
            return Name == other.Name && Equals(LODGroup, other.LODGroup);
        }

        public override bool Equals(object obj)
        {
            if (obj is null) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((GPUInstancingLODGroupWithBuffer) obj);
        }

        public override int GetHashCode() =>
            HashCode.Combine(Name, LODGroup);
    }

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
                Matrix4x4 localToRootMatrix = transform.worldToLocalMatrix * lodGroup.transform.localToWorldMatrix; // root * child
                TryAddToCollected(lodGroup, localToRootMatrix);
                lodGroup.HideAll();
            }

            foreach (KeyValuePair<GPUInstancingLODGroup,List<PerInstanceBuffer>> pair in candidatesTable)
                IndirectCandidates.Add(new GPUInstancingLODGroupWithBuffer(pair.Key, pair.Value));
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
