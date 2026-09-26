using ECS.Prioritization;
using System;
using System.Collections.Generic;

namespace Global.Static
{
    internal class NoPartitionSettings : IPartitionSettings
    {
        public float AngleTolerance => float.MaxValue;
        public float PositionSqrTolerance => float.MaxValue;
        public IReadOnlyList<int> SqrDistanceBuckets { get; } = Array.Empty<int>();
        public int FastPathSqrDistance => 0;
    }
}
