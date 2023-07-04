using DCL.ECSComponents;

namespace ECS.Prioritization.Components
{
    /// <summary>
    ///     Partition data assigned to the entity.
    ///     <para>All entities that has a visual representation must be assigned a partition in order to be weighed against each other</para>
    ///     <para>Partitioning should happen as a first step before all systems can ever execute any logic</para>
    /// </summary>
    public struct PartitionComponent : IDirtyMarker
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
    }
}
