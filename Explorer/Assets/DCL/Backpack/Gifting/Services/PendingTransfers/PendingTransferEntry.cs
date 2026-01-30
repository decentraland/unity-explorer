using System;

namespace DCL.Backpack.Gifting.Services.PendingTransfers
{
    /// <summary>
    /// Represents a pending transfer with timestamp for accurate pruning.
    /// </summary>
    public readonly struct PendingTransferEntry : IEquatable<PendingTransferEntry>
    {
        /// <summary>
        /// The full URN including token ID (e.g., urn:decentraland:matic:collections-v2:0x...:tokenId)
        /// </summary>
        public string FullUrn { get; }
        
        /// <summary>
        /// UTC timestamp when the transfer was initiated.
        /// Used to detect if an item returned after we sent it (A→B→A scenario).
        /// </summary>
        public DateTime SentAtUtc { get; }

        public PendingTransferEntry(string fullUrn, DateTime sentAtUtc)
        {
            FullUrn = fullUrn;
            SentAtUtc = sentAtUtc;
        }

        public PendingTransferEntry(string fullUrn) : this(fullUrn, DateTime.UtcNow) { }

        public bool Equals(PendingTransferEntry other) =>
            string.Equals(FullUrn, other.FullUrn, StringComparison.OrdinalIgnoreCase);

        public override bool Equals(object? obj) =>
            obj is PendingTransferEntry other && Equals(other);

        public override int GetHashCode() =>
            FullUrn?.ToLowerInvariant().GetHashCode() ?? 0;

        public override string ToString() =>
            $"PendingTransferEntry({FullUrn}, SentAt: {SentAtUtc:O})";
    }
}
