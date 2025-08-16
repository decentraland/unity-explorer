using System;
using System.Collections.Generic;

namespace DCL.Rendering.GPUInstancing.InstancingData
{
    [Serializable]
    public class GPUInstancingLODGroupWithBuffer : IEquatable<GPUInstancingLODGroupWithBuffer>
    {
        public string Name;
        public CombinedLODGroupData combinedLODGroupData;
        public List<PerInstanceBuffer> InstancesBuffer;

        public LODGroupData LODGroupData;
        public List<CombinedLodsRenderer> CombinedLodsRenderers;

        public GPUInstancingLODGroupWithBuffer(CombinedLODGroupData combinedLODGroupData, List<PerInstanceBuffer> instances)
        {
            Name = combinedLODGroupData.Name;
            this.combinedLODGroupData = combinedLODGroupData;

            LODGroupData = combinedLODGroupData.LODGroupData;
            CombinedLodsRenderers = this.combinedLODGroupData.CombinedLodsRenderers;
            InstancesBuffer = instances;
        }

        // Конструктор для создания объединенных групп с единственным рендерером
        public GPUInstancingLODGroupWithBuffer(string name, LODGroupData lodGroupData, CombinedLodsRenderer renderer, List<PerInstanceBuffer> instances)
        {
            Name = name;
            combinedLODGroupData = null; // Не используется для объединенных групп

            LODGroupData = lodGroupData;
            CombinedLodsRenderers = new List<CombinedLodsRenderer> { renderer };
            InstancesBuffer = instances;
        }

        public bool Equals(GPUInstancingLODGroupWithBuffer other)
        {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return true;

            return Name == other.Name &&
                   Equals(combinedLODGroupData, other.combinedLODGroupData) &&
                   LODGroupData.Equals(other.LODGroupData);
        }

        public override bool Equals(object obj) =>
            obj is GPUInstancingLODGroupWithBuffer other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine(Name, combinedLODGroupData, LODGroupData);
    }
}
