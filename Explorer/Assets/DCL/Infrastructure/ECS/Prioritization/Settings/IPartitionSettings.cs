using System.Collections.Generic;

namespace ECS.Prioritization
{
    /// <summary>
    ///     An abstract way to retrieve partition settings
    /// </summary>
    public interface IPartitionSettings
    {
        /// <summary>
        ///     Maximum tolerance between the last processed camera rotation and the current one to skip re-partitioning, default = 1
        /// </summary>
        float AngleTolerance { get; }

        /// <summary>
        ///     Maximum tolerance between the last processed camera position and the current one to skip re-partitioning, default = 10cm x 10cm
        /// </summary>
        float PositionSqrTolerance { get; }

        /// <summary>
        ///     Buckets squared without the first 0, e.g. [128; 512; 2048...]
        /// </summary>
        IReadOnlyList<int> SqrDistanceBuckets { get; }

        /// <summary>
        ///     Distance to the player camera from which the calculation of the partition is simplified
        /// </summary>
        int FastPathSqrDistance { get; }
    }
}
