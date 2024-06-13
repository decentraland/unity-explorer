using System;

namespace ECS.Prioritization.Components
{
    /// <summary>
    ///     Partition data assigned to the entity.
    ///     <para>All entities that has a visual representation must be assigned a partition in order to be weighed against each other</para>
    ///     <para>Partitioning should happen as a first step before all systems can ever execute any logic</para>
    /// </summary>
    public class PartitionComponent : IPartitionComponent, IEquatable<IPartitionComponent>
    {
        public static readonly PartitionComponent TOP_PRIORITY = new ()
        {
            Bucket = 0,
            IsBehind = false,
            IsPlayerInScene = false,
        };

        /// <summary>
        ///     Indicates that the partition is out of buckets range
        /// </summary>
        public bool OutOfRange { get; set; }

        /// <summary>
        ///     Indicates that the partition value has changed and the processes assigned to it should be re-prioritized
        /// </summary>
        public bool IsDirty { get; set; }

        /// <summary>
        ///     Raw Square Distance from camera to entity based on which the bucket is calculated
        /// </summary>
        public float RawSqrDistance { get; set; }

        /// <summary>
        ///     Each entity falls into one of the buckets within the predefined range of values.
        ///     The higher value of bucket is the less priority the processing of the entity should be given
        /// </summary>
        public byte Bucket { get; set; }

        /// <summary>
        ///     Indicates if entity position is counted as behind the forward vector of the camera
        /// </summary>
        public bool IsBehind { get; set; }
        public bool IsPlayerInScene { get; set; }

        public bool Equals(IPartitionComponent other) =>
            Bucket == other.Bucket && IsBehind == other.IsBehind;

        public override bool Equals(object obj) =>
            obj is PartitionComponent other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine(Bucket, IsBehind);

        public override string ToString() =>
            $"Partition: (Bucket {Bucket.ToString()}, IsBehind {IsBehind.ToString()})";
    }
}
