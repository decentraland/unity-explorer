namespace CRDT.Protocol
{
    /// <summary>
    ///     The effect that should be applied to the system followed by
    ///     the reconciliation of the particular message.
    ///     It is the concrete instruction for the ECS system
    /// </summary>
    public enum CRDTReconciliationEffect : byte
    {
        /// <summary>
        ///     No changes to the local state required
        /// </summary>
        NoChanges = 0,

        /// <summary>
        ///     Entity deleted by the current message
        /// </summary>
        EntityDeleted = 1,

        /// <summary>
        ///     Component added
        /// </summary>
        ComponentAdded = 2,

        /// <summary>
        ///     Existing component modified
        /// </summary>
        ComponentModified = 3,

        /// <summary>
        ///     Component deleted by the current message
        /// </summary>
        ComponentDeleted = 4,
    }
}
