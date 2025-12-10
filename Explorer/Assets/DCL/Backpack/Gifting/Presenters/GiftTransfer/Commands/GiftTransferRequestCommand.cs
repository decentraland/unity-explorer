using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.Backpack.Gifting.Events;
using DCL.Backpack.Gifting.Services;
using DCL.Backpack.Gifting.Services.PendingTransfers;
using DCL.Backpack.Gifting.Views;
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
        
        public GiftTransferRequestCommand(IEventBus eventBus,
            IWeb3IdentityCache web3IdentityCache,
            IGiftTransferService giftTransferService,
            IPendingTransferService pendingTransferService)
        {
            this.eventBus = eventBus;
            this.web3IdentityCache = web3IdentityCache;
            this.giftTransferService = giftTransferService;
            this.pendingTransferService = pendingTransferService;
        }

        public async UniTask<GiftTransferResult> ExecuteAsync(GiftTransferParams data, CancellationToken ct)
        {
            eventBus.Publish(new GiftingEvents.GiftTransferProgress(data.giftUrn,
                GiftingEvents.GiftTransferPhase.Authorizing,
                GiftingTextIds.WaitingForWalletMessage
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
                eventBus.Publish(new GiftingEvents.OnCanceledGift(data.giftUrn, senderAddress, data.recipientAddress, data.itemType));
                return GiftTransferResult.Fail(MsgCancelShort);
            }

            if (result.IsSuccess)
            {
                pendingTransferService.AddPending(data.instanceUrn);

                eventBus.Publish(new GiftingEvents.GiftTransferSucceeded(data.giftUrn));
                eventBus.Publish(new GiftingEvents.OnSuccessfulGift(data.giftUrn, senderAddress, data.recipientAddress, data.itemType));
            }
            else
            {
                eventBus.Publish(new GiftingEvents.GiftTransferFailed(data.giftUrn, result.ErrorMessage));
                eventBus.Publish(new GiftingEvents.OnFailedGift(data.giftUrn, senderAddress, data.recipientAddress, data.itemType));
            }

            return result;
        }
    }
}