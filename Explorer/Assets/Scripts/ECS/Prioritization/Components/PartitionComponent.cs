using DCL.ECSComponents;
using System;

namespace ECS.Prioritization.Components
{
    /// <summary>
    ///     Partition data assigned to the entity.
    ///     <para>All entities that has a visual representation must be assigned a partition in order to be weighed against each other</para>
    ///     <para>Partitioning should happen as a first step before all systems can ever execute any logic</para>
    /// </summary>
    public struct PartitionComponent : IDirtyMarker, IComparable<PartitionComponent>
    {
        /// <summary>
        ///     The maximum value of <see cref="Bucket" />
        /// </summary>
        public const byte MAX_BUCKET = 10;

        /// <summary>
        ///     Each entity falls into one of the buckets within the predefined range of values.
        ///     The higher value of bucket is the less priority the processing of the entity should be given
        /// </summary>
        public byte Bucket;

        /// <summary>
        ///     Indicates if entity position is counted as behind the forward vector of the camera
        /// </summary>
        public bool IsBehind;

        /// <summary>
        ///     Indicates that the partition value has changed and the processes assigned to it should be re-prioritized
        /// </summary>
        public bool IsDirty { get; set; }

        public int CompareTo(PartitionComponent other)
        {
            // First, compare the IsBehind values. We want to 'reverse' it, meaning that
            // the false value should always have priority over the true value
            int boolComparison = IsBehind.CompareTo(other.IsBehind);

            if (boolComparison != 0)
                return boolComparison;

            // If the IsBehind values are the same, compare the int values
            return Bucket.CompareTo(other.Bucket);
        }
    }

}
