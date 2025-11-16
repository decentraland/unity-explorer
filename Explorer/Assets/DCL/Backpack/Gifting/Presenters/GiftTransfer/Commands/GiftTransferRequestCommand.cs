using System.Threading;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Backpack.Gifting.Cache;
using DCL.Backpack.Gifting.Events;
using DCL.Backpack.Gifting.Services;
using DCL.Backpack.Gifting.Views;
using DCL.Web3.Identities;
using Utility;

namespace DCL.Backpack.Gifting.Presenters.GiftTransfer.Commands
{
    public class GiftTransferRequestCommand
    {
        private readonly IEventBus eventBus;
        private readonly IGiftTransferService giftTransferService;
        private readonly IWeb3IdentityCache  web3IdentityCache;

        public GiftTransferRequestCommand(IEventBus eventBus,
            IWeb3IdentityCache web3IdentityCache,
            IGiftTransferService giftTransferService)
        {
            this.eventBus = eventBus;
            this.web3IdentityCache = web3IdentityCache;
            this.giftTransferService = giftTransferService;
        }

        public async UniTaskVoid ExecuteAsync(GiftTransferParams data, CancellationToken ct)
        {
            eventBus.Publish(new GiftingEvents.GiftTransferProgress(data.giftUrn,
                GiftingEvents.GiftTransferPhase.Authorizing,
                "Opening wallet to authorize…"
            ));

            var identity = web3IdentityCache.Identity;
            if (identity == null)
            {
                eventBus.Publish(new GiftingEvents.GiftTransferFailed(data.giftUrn, "User identity not found."));
                return;
            }

            string senderAddress = identity.Address.ToString();

            var result = await giftTransferService
                .RequestTransferAsync(senderAddress,
                    data.giftUrn,
                    data.tokenId,
                    data.recipientAddress, ct);
            
            if (ct.IsCancellationRequested)
            {
                eventBus.Publish(new GiftingEvents.GiftTransferFailed(data.giftUrn, "Gifting was cancelled."));
                return;
            }

            if (result.IsSuccess)
            {
                PendingGiftsCache.Add(new URN(data.giftUrn));
                
                eventBus.Publish(new GiftingEvents.GiftTransferSucceeded(data.giftUrn));
                eventBus.Publish(new GiftingEvents.OnSuccessfulGift(data.giftUrn, senderAddress, data.recipientAddress, data.itemType));
            }
            else
            {
                eventBus.Publish(new GiftingEvents.GiftTransferFailed(data.giftUrn, result.ErrorMessage));
                eventBus.Publish(new GiftingEvents.OnFailedGift(data.giftUrn, senderAddress, data.recipientAddress, data.itemType));
            }
        }
    }
}