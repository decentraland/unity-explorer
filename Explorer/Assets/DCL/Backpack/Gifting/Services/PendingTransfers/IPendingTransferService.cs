using System;
using DCL.AvatarRendering.Loading;
using DCL.Backpack.Gifting.Models;

namespace DCL.Backpack.Gifting.Services.PendingTransfers
{
    public interface IPendingTransferService : IOwnedNftFilter
    {
        /// <param name="fullUrn">Full URN (token instance) that has been gifted away.</param>
        /// <param name="baselineTransferredAt">
        ///     The gifted copy's last transfer-in timestamp at the moment of gifting. Used by
        ///     <see cref="Prune" /> to tell an indexer that has not caught up yet apart from an item
        ///     that left the wallet and was later transferred back.
        /// </param>
        /// <param name="kind">Whether the gifted item is a wearable or an emote.</param>
        void AddPending(string fullUrn, DateTime baselineTransferredAt, GiftableType kind);

        bool IsPending(string fullUrn);
        int GetPendingCount(string baseUrn);

        /// <summary>
        ///     Drops pending transfers of the given <paramref name="kind" /> that the matching owned-NFT
        ///     registry confirms have either left the wallet or been transferred back in. Call right after the
        ///     corresponding inventory was (re)fetched, so the registry it consults is authoritative — that is
        ///     what lets a missing entry be read as "left the wallet" rather than "not loaded yet".
        /// </summary>
        void Prune(GiftableType kind);

        void LogPendingTransfers();
    }
}
