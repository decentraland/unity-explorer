using System;
using DCL.AvatarRendering.Loading;
using DCL.Backpack.Gifting.Models;

namespace DCL.Backpack.Gifting.Services.PendingTransfers
{
    public interface IPendingTransferService : IOwnedNftFilter
    {
        /// <param name="fullUrn">Full URN (token instance) that has been gifted away.</param>
        /// <param name="baselineTransferredAt">
        ///     The gifted copy's transfer-in timestamp at gift time. Lets <see cref="Prune" /> tell an indexer
        ///     that hasn't caught up from an item that left and was later transferred back.
        /// </param>
        /// <param name="kind">Whether the gifted item is a wearable or an emote.</param>
        void AddPending(string fullUrn, DateTime baselineTransferredAt, GiftableType kind);

        bool IsPending(string fullUrn);
        int GetPendingCount(string baseUrn);

        /// <summary>
        ///     Drops pending transfers of <paramref name="kind" /> that the matching owned-NFT registry confirms
        ///     left the wallet or came back. Call right after that inventory was (re)fetched, so a missing entry
        ///     reads as "left the wallet" rather than "not loaded yet".
        /// </summary>
        void Prune(GiftableType kind);

        void LogPendingTransfers();
    }
}
