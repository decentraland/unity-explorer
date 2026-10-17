namespace ECS.Prioritization.Components
{
    /// <summary>
    ///     A read-only way to transfer and sync partition data between scenes and promises.
    ///     <para>
    ///         It's a ref type so no additional mechanism is required to access the most actual value.
    ///         Using the interface means that the value is inherited from the upstream system
    ///     </para>
    /// </summary>
    public interface IPartitionComponent
    {
        /// <summary>
        ///     Each entity falls into one of the buckets within the predefined range of values.
        ///     The higher value of bucket is the less priority the processing of the entity should be given
        /// </summary>
        byte Bucket { get; }

        /// <summary>
        ///     Indicates if entity position is counted as behind the forward vector of the camera
        /// </summary>
        public bool IsBehind { get; }

        bool IsDirty { get; }

        float RawSqrDistance { get; }
    }
}
