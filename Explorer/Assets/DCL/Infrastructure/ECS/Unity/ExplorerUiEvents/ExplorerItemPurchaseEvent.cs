using DCL.ECSComponents;
using System;

namespace ECS.Unity.ExplorerUiEvents
{
    public enum ExplorerItemPurchaseStatus
    {
        Purchased,
        Dismissed,
        Failed,
    }

    /// <summary>
    ///     The outcome of an item purchase a scene asked for through <c>openExplorerUi</c>, waiting to be
    ///     written to that scene as a <see cref="PBExplorerItemPurchaseResult" />. Queued and drained on the
    ///     main thread, like <see cref="ExplorerUiEvent" />.
    /// </summary>
    public readonly struct ExplorerItemPurchaseEvent : IEquatable<ExplorerItemPurchaseEvent>
    {
        public readonly string Urn;
        public readonly ExplorerItemPurchaseStatus Status;

        /// <summary>Id of the request that produced this event, 0 when that request carried none.</summary>
        public readonly uint RequestId;

        /// <summary>Scene tick the purchase flow ended on, which is earlier than the tick it is written out on.</summary>
        public readonly uint Tick;

        public ExplorerItemPurchaseEvent(string urn, ExplorerItemPurchaseStatus status, uint requestId, uint tick)
        {
            Urn = urn;
            Status = status;
            RequestId = requestId;
            Tick = tick;
        }

        public bool Equals(ExplorerItemPurchaseEvent other) =>
            Urn == other.Urn && Status == other.Status && RequestId == other.RequestId && Tick == other.Tick;

        public override bool Equals(object? obj) =>
            obj is ExplorerItemPurchaseEvent other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine(Urn, (int)Status, RequestId, Tick);
    }
}
