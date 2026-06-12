using CRDT;

namespace CrdtEcsBridge.OutgoingMessages
{
    /// <summary>
    ///     (Entity, ComponentId) packed into a single long so the pending-messages dictionary
    ///     uses the devirtualized default comparer instead of a comparer-class dispatch with
    ///     <see cref="System.HashCode.Combine{T1, T2}" /> per probe
    /// </summary>
    internal static class OutgoingMessageKey
    {
        public static long Pack(CRDTEntity entity, int componentId) =>
            ((long)componentId << 32) | (uint)entity.Id;
    }
}
