using System.Threading;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Emotes;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Backpack.Gifting.Events;
using DCL.Backpack.Gifting.Services;
using DCL.Backpack.Gifting.Services.GiftingInventory;
using DCL.Backpack.Gifting.Services.PendingTransfers;
using DCL.Backpack.Gifting.Views;
using DCL.Diagnostics;
using DCL.Web3.Authenticators;
using DCL.Web3.Identities;
using Utility;

namespace DCL.Backpack.Gifting.Presenters.GiftTransfer.Commands
{
    public class GiftTransferRequestCommand
    {
        private const string MsgIdentityNotFound = "User identity not found.";
        private const string MsgCancelUser = "Gifting was canceled.";
        private const string MsgCancelShort = "Canceled";

        private readonly IEventBus eventBus;
        private readonly IGiftTransferService giftTransferService;
        private readonly IWeb3IdentityCache  web3IdentityCache;
        private readonly IPendingTransferService pendingTransferService;
        private readonly ICompositeWeb3Provider web3Provider;
        private readonly IWearableStorage wearableStorage;
        private readonly IEmoteStorage emoteStorage;

        public GiftTransferRequestCommand(IEventBus eventBus,
            IWeb3IdentityCache web3IdentityCache,
            IGiftTransferService giftTransferService,
            IPendingTransferService pendingTransferService,
            ICompositeWeb3Provider web3Provider,
            IWearableStorage wearableStorage,
            IEmoteStorage emoteStorage)
        {
            this.eventBus = eventBus;
            this.web3IdentityCache = web3IdentityCache;
            this.giftTransferService = giftTransferService;
            this.pendingTransferService = pendingTransferService;
            this.web3Provider = web3Provider;
            this.wearableStorage = wearableStorage;
            this.emoteStorage = emoteStorage;
        }

        public string GetWaitingMessage() =>
            web3Provider.IsThirdWebOTP
                ? GiftingTextIds.WaitingForWalletMessageThirdWeb
                : GiftingTextIds.WaitingForWalletMessage;

        public async UniTask<GiftTransferResult> ExecuteAsync(GiftTransferParams data, CancellationToken ct)
        {
            eventBus.Publish(new GiftingEvents.GiftTransferProgress(data.giftUrn,
                GiftingEvents.GiftTransferPhase.Authorizing,
                GetWaitingMessage()
            ));

            var identity = web3IdentityCache.Identity;
            if (identity == null)
            {
                string error = MsgIdentityNotFound;
                eventBus.Publish(new GiftingEvents.GiftTransferFailed(data.giftUrn, error));
                return GiftTransferResult.Fail(error);
            }

            string senderAddress = identity.Address.ToString();

            eventBus.Publish(new GiftingEvents.OnSentGift(data.giftUrn,
                data.instanceUrn,
                senderAddress,
                data.recipientAddress,
                data.itemType));

            var result = await giftTransferService
                .RequestTransferAsync(senderAddress,
                    data.giftUrn,
                    data.tokenId,
                    data.recipientAddress, ct);

            if (ct.IsCancellationRequested)
            {
                eventBus.Publish(new GiftingEvents.GiftTransferFailed(data.giftUrn, MsgCancelUser));
                eventBus.Publish(new GiftingEvents.OnCanceledGift(data.giftUrn, data.instanceUrn, senderAddress, data.recipientAddress, data.itemType));
                return GiftTransferResult.Fail(MsgCancelShort);
            }

            if (result.IsSuccess)
            {
                pendingTransferService.AddPending(data.instanceUrn);
                EvictOwnedNftFromRegistry(data.instanceUrn, data.itemType);

                eventBus.Publish(new GiftingEvents.GiftTransferSucceeded(data.giftUrn));
                eventBus.Publish(new GiftingEvents.OnSuccessfulGift(data.giftUrn, data.instanceUrn, senderAddress, data.recipientAddress, data.itemType));
            }
            else
            {
                eventBus.Publish(new GiftingEvents.GiftTransferFailed(data.giftUrn, result.ErrorMessage));
                eventBus.Publish(new GiftingEvents.OnFailedGift(data.giftUrn, data.instanceUrn, senderAddress, data.recipientAddress, data.itemType));
            }

            return result;
        }

        // Local successful transfers are not yet reflected by the indexer. Evict the gifted token from the owned-NFT registry
        // so that profile deploys don't pick a tokenId the catalyst will reject as not-owned.
        private void EvictOwnedNftFromRegistry(string instanceUrn, string itemType)
        {
            if (string.IsNullOrEmpty(instanceUrn)) return;

            var fullUrn = new URN(instanceUrn);
            URN baseUrn = fullUrn.Shorten();

            // Off-chain or already-short URN: no token tail to evict
            if (baseUrn.Equals(fullUrn)) return;

            switch (itemType?.ToLowerInvariant())
            {
                case GiftingItemTypes.Wearable:
                    wearableStorage.RemoveOwnedNft(baseUrn, fullUrn);
                    break;
                case GiftingItemTypes.Emote:
                    emoteStorage.RemoveOwnedNft(baseUrn, fullUrn);
                    break;
                default:
                    ReportHub.LogWarning(ReportCategory.GIFTING, $"[GiftTransferRequestCommand] Unknown itemType '{itemType}' for {instanceUrn}; skipping registry eviction.");
                    break;
            }
        }
    }
}