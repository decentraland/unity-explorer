using System;
using DCL.Backpack.Gifting.Models;

namespace DCL.Backpack.Gifting.Services.PendingTransfers
{
    /// <summary>
    ///     A locally-tracked gift whose on-chain transfer has been initiated but not yet confirmed by the indexer.
    /// </summary>
    public readonly struct PendingTransfer
    {
        /// <summary>
        ///     The gifted copy's transfer-in timestamp at gift time. Lets pruning tell an indexer that hasn't
        ///     caught up (same timestamp) from one that left and came back (newer). <see cref="DateTime.MinValue" /> = unknown.
        /// </summary>
        public readonly DateTime BaselineTransferredAt;

        /// <summary>
        ///     Wearable or emote, so pruning only consults the matching registry.
        ///     Null for legacy entries from before the type was tracked; those aren't scope-pruned.
        /// </summary>
        public readonly GiftableType? Kind;

        public PendingTransfer(DateTime baselineTransferredAt, GiftableType? kind)
        {
            BaselineTransferredAt = baselineTransferredAt;
            Kind = kind;
        }
    }
}
