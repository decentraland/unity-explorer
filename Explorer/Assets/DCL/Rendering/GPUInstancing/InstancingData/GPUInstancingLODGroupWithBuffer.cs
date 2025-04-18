﻿using System;
using System.Collections.Generic;

namespace DCL.Rendering.GPUInstancing.InstancingData
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

        public override bool Equals(object obj) =>
            obj is GPUInstancingLODGroupWithBuffer other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine(Name, LODGroup);
    }
}
