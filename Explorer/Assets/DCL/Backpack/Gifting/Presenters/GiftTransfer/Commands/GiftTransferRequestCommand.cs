using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.Backpack.Gifting.Events;
using DCL.Backpack.Gifting.Views;
using Utility;

namespace DCL.Backpack.Gifting.Presenters.GiftTransfer.Commands
{
    public class GiftTransferRequestCommand
    {
        private readonly IEventBus eventBus;

        public GiftTransferRequestCommand(IEventBus eventBus)
        {
            this.eventBus = eventBus;
        }

        public async UniTaskVoid ExecuteAsync(GiftTransferStatusParams data,
            CancellationToken ct)
        {
            eventBus.Publish(new GiftingEvents.GiftTransferProgress(
                data.giftUrn,
                GiftingEvents.GiftTransferPhase.Authorizing,
                "Opening wallet to authorize…"
            ));

            await UniTask.Delay(TimeSpan.FromMilliseconds(300), cancellationToken: ct);

            // TODO: trigger browser/wallet open using dependencies you’ll inject later

            // optional: if you want to immediately reflect “Broadcasting” after wallet handoff
            // eventBus.Publish(new GiftingEvents.GiftTransferProgress(
            //     data.giftUrn,
            //     GiftingEvents.GiftTransferPhase.Broadcasting,
            //     "Transaction broadcasted…"
            // ));
        }
    }
}