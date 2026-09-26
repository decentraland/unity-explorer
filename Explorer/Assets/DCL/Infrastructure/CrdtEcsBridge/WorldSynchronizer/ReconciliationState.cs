using CRDT.Protocol;
using System;

namespace CrdtEcsBridge.WorldSynchronizer
{
    internal readonly struct ReconciliationState : IEquatable<ReconciliationState>
    {
        /// <summary>
        ///     Represents the status of the (entity, component) at the start of the batch
        /// </summary>
        public readonly CRDTReconciliationEffect First;

        /// <summary>
        ///     Represents the most recent command
        /// </summary>
        public readonly CRDTReconciliationEffect Last;

        internal ReconciliationState(CRDTReconciliationEffect first, CRDTReconciliationEffect last)
        {
            First = first;
            Last = last;
        }

        public bool Equals(ReconciliationState other) =>
            First == other.First && Last == other.Last;

        public override bool Equals(object obj) =>
            obj is ReconciliationState other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine((int)First, (int)Last);

        public override string ToString() =>
            $"First: {First}, Last: {Last}";

        public static implicit operator ReconciliationState((CRDTReconciliationEffect, CRDTReconciliationEffect) tuple) =>
            new (tuple.Item1, tuple.Item2);
    }
}
