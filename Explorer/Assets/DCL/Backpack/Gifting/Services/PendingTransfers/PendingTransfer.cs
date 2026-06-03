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
        ///     The gifted copy's last transfer-in timestamp at the moment of gifting. Lets pruning tell an
        ///     indexer that has not caught up yet (same timestamp) apart from an item that left the wallet and
        ///     was later transferred back (a newer timestamp). <see cref="DateTime.MinValue" /> means unknown.
        /// </summary>
        public readonly DateTime BaselineTransferredAt;

        /// <summary>
        ///     Whether the gifted item is a wearable or an emote, so pruning only consults the matching registry.
        ///     Null for legacy entries persisted before the type was tracked; those are not scope-pruned.
        /// </summary>
        public readonly GiftableType? Kind;

        public PendingTransfer(DateTime baselineTransferredAt, GiftableType? kind)
        {
            BaselineTransferredAt = baselineTransferredAt;
            Kind = kind;
        }
    }
}
