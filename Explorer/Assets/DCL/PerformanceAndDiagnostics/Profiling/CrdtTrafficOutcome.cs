namespace DCL.Profiling
{
    /// <summary>
    ///     What a single CRDT message did to the local CRDT state. Everything but
    ///     <see cref="Applied" /> is wasted work: the sender paid serialization and the receiver
    ///     paid deserialization for a message that changed nothing.
    /// </summary>
    public enum CrdtTrafficOutcome : byte
    {
        /// <summary>
        ///     The message changed the local state.
        /// </summary>
        Applied = 0,

        /// <summary>
        ///     The message carried a newer timestamp but a payload identical to the stored one:
        ///     the sender re-wrote a component without changing its values.
        /// </summary>
        RedundantIdenticalData = 1,

        /// <summary>
        ///     Same timestamp and same payload as the stored state.
        /// </summary>
        NoOpSameState = 2,

        /// <summary>
        ///     Older timestamp, or same timestamp and lower payload: discarded by last-write-wins.
        /// </summary>
        NoOpOutdated = 3,

        /// <summary>
        ///     The message targets an entity that was already deleted.
        /// </summary>
        NoOpEntityDeleted = 4,

        /// <summary>
        ///     Dropped before reaching the protocol: a Creator Hub component leaked from
        ///     <c>main.composite</c>.
        /// </summary>
        FilteredCreatorHub = 5,
    }
}
