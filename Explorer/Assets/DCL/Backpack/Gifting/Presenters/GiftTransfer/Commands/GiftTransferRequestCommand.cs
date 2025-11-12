using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.Backpack.Gifting.Events;
using DCL.Backpack.Gifting.Services;
using DCL.Backpack.Gifting.Views;
using DCL.Diagnostics;
using Utility;

namespace DCL.Backpack.Gifting.Presenters.GiftTransfer.Commands
{
    public class GiftTransferRequestCommand
    {
        private readonly IEventBus eventBus;
        private readonly IGiftTransferService giftTransferService;

        public GiftTransferRequestCommand(IEventBus eventBus, IGiftTransferService giftTransferService)
        {
            this.eventBus = eventBus;
            this.giftTransferService = giftTransferService;
        }

        public async UniTaskVoid ExecuteAsync(GiftTransferParams data, CancellationToken ct)
        {
            // 1. Inform UI the process is starting
            eventBus.Publish(new GiftingEvents.GiftTransferProgress(
                data.giftUrn,
                GiftingEvents.GiftTransferPhase.Authorizing,
                "Opening wallet to authorize…"
            ));

            giftTransferService.OnVerificationCodeReceived += (i, time) =>
            {
                ReportHub.Log(ReportCategory.GIFTING, $"gift service {i}-{time}");
            };
            
            // 2. Call the service. The 'TODO' is now resolved.
            // This single line handles everything: browser opening, waiting for signature, and getting the result.
            var result = await giftTransferService.RequestTransferAsync(data.giftUrn, data.recipientAddress, ct);

            // 3. Handle the result
            if (ct.IsCancellationRequested)
            {
                eventBus.Publish(new GiftingEvents.GiftTransferFailed(data.giftUrn, "Gifting was cancelled."));
                return;
            }

            if (result.IsSuccess)
                eventBus.Publish(new GiftingEvents.GiftTransferSucceeded(data.giftUrn));
            else
                eventBus.Publish(new GiftingEvents.GiftTransferFailed(data.giftUrn, result.ErrorMessage));
        }
    }
}