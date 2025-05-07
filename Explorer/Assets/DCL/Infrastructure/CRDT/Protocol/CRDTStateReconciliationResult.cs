namespace CRDT.Protocol
{
    /// <summary>
    ///     This result exists to comply with the ADR
    ///     and have an extended info for debugging
    /// </summary>
    public enum CRDTStateReconciliationResult : byte
    {
        /**
         * Typical message and new state set.
         * @state CHANGE
         * @reason Incoming message has a timestamp greater
         */
        StateUpdatedTimestamp = 1,

        /**
         * Typical message when it is considered old.
         * @state it does NOT CHANGE.
         * @reason incoming message has a timestamp lower.
         */
        StateOutdatedTimestamp = 2,

        /**
         * Weird message, same timestamp and data.
         * @state it does NOT CHANGE.
         * @reason consistent state between peers.
         */
        NoChanges = 3,

        /**
         * Less but typical message, same timestamp, resolution by data.
         * @state it does NOT CHANGE.
         * @reason incoming message has a LOWER data.
         */
        StateOutdatedData = 4,

        /**
         * Less but typical message, same timestamp, resolution by data.
         * @state CHANGE
         * @reason incoming message has a GREATER data.
         */
        StateUpdatedData = 5,

        /**
         * Entity was previously deleted.
         * @state it does NOT CHANGE.
         * @reason The message is considered old.
         */
        EntityWasDeleted = 6,

        /**
         * Entity should be deleted.
         * @state CHANGE
         * @reason the state is storing old entities
         */
        EntityDeleted = 7,

        /**
         * An APPEND with a success insert
         * @state CHANGE
         * @reason the element is not in set set yet
         */
        StateAppendedData = 8,
    }
}
