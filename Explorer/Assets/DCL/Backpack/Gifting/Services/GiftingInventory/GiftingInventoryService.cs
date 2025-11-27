using System.Collections.Generic;
using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Emotes;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Backpack.Gifting.Services.PendingTransfers;
using DCL.Backpack.Gifting.Services.SnapshotEquipped;

namespace DCL.Backpack.Gifting.Services.GiftingInventory
{
    public class GiftInventoryService
    {
        private readonly IWearableStorage wearableStorage;
        private readonly IEmoteStorage emoteStorage;
        private readonly IAvatarEquippedStatusProvider equippedStatusProvider;
        private readonly IPendingTransferService pendingTransferService;

        public GiftInventoryService(
            IWearableStorage wearableStorage,
            IEmoteStorage emoteStorage,
            IAvatarEquippedStatusProvider equippedStatusProvider,
            IPendingTransferService pendingTransferService)
        {
            this.wearableStorage = wearableStorage;
            this.emoteStorage = emoteStorage;
            this.equippedStatusProvider = equippedStatusProvider;
            this.pendingTransferService = pendingTransferService;
        }

        public bool TryGetBestTransferableToken(string itemUrn, string itemType, out string tokenId, out string instanceUrn)
        {
            tokenId = "0";
            instanceUrn = string.Empty;
            URN baseUrnObj = new URN(itemUrn);

            IReadOnlyDictionary<URN, NftBlockchainOperationEntry> ownedCopies;

            if (itemType == "wearable")
            {
                if (!wearableStorage.TryGetOwnedNftRegistry(baseUrnObj, out ownedCopies)) return false;
            }
            else
            {
                if (!emoteStorage.TryGetOwnedNftRegistry(baseUrnObj, out ownedCopies)) return false;
            }

            NftBlockchainOperationEntry? bestCandidate = null;

            foreach (var entry in ownedCopies.Values)
            {
                string fullUrnString = entry.Urn;

                if (!equippedStatusProvider.IsEquipped(fullUrnString) &&
                    !pendingTransferService.IsPending(fullUrnString))
                {
                    // Pick the most recently acquired one
                    if (bestCandidate == null || entry.TransferredAt > bestCandidate.Value.TransferredAt)
                        bestCandidate = entry;
                }
            }

            if (bestCandidate != null)
            {
                tokenId = bestCandidate.Value.TokenId;
                instanceUrn = bestCandidate.Value.Urn;
                return true;
            }

            return false;
        }
    }
}