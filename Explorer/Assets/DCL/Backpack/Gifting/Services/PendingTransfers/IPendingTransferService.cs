using System.Collections.Generic;
using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Wearables.Components;

namespace DCL.Backpack.Gifting.Services.PendingTransfers
{
    public interface IPendingTransferService
    {
        void AddPending(string fullUrn);
        bool IsPending(string fullUrn);
        int GetPendingCount(string baseUrn);

        void Prune(
            IReadOnlyDictionary<URN, Dictionary<URN, NftBlockchainOperationEntry>> wearableRegistry,
            IReadOnlyDictionary<URN, Dictionary<URN, NftBlockchainOperationEntry>> emoteRegistry);

        void LogPendingTransfers();
    }
}