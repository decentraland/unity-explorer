using System.Collections.Generic;
using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Wearables.Components;

namespace DCL.Backpack.Gifting.Services.PendingTransfers
{
    public interface IPendingTransferService
    {
        /// <summary>
        /// Add a wearable to pending transfers with current UTC timestamp.
        /// </summary>
        void AddPendingWearable(string fullUrn);

        /// <summary>
        /// Add an emote to pending transfers with current UTC timestamp.
        /// </summary>
        void AddPendingEmote(string fullUrn);

        /// <summary>
        /// Check if a URN (wearable or emote) is pending transfer.
        /// </summary>
        bool IsPending(string fullUrn);

        /// <summary>
        /// Get count of pending transfers for a base URN (checks both wearables and emotes).
        /// </summary>
        int GetPendingCount(string baseUrn);

        /// <summary>
        /// Prune wearable pending transfers based on registry state.
        /// Call this after fetching wearables from API.
        /// </summary>
        void PruneWearables(
            IReadOnlyDictionary<URN, Dictionary<URN, NftBlockchainOperationEntry>> wearableRegistry);

        /// <summary>
        /// Prune emote pending transfers based on registry state.
        /// Call this after fetching emotes from API.
        /// </summary>
        void PruneEmotes(
            IReadOnlyDictionary<URN, Dictionary<URN, NftBlockchainOperationEntry>> emoteRegistry);

        /// <summary>
        /// Log all pending transfers for debugging.
        /// </summary>
        void LogPendingTransfers();
    }
}
