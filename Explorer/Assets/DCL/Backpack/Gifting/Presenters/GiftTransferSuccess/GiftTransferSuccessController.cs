using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.Backpack.Gifting.Views;
using MVC;
using Utility;

namespace DCL.Backpack.Gifting.Presenters
{
    public sealed class GiftTransferSuccessController
        : ControllerBase<GiftTransferSuccessView, GiftTransferSuccessParams>
    {
        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Popup;

        private CancellationTokenSource? lifeCts;

        public GiftTransferSuccessController(ViewFactoryMethod viewFactory) : base(viewFactory)
        {
        }

        protected override void OnViewInstantiated()
        {
            if (viewInstance == null) return;
        }

        protected override void OnViewShow()
        {
            lifeCts = new CancellationTokenSource();

            if (viewInstance.GiftSentText != null)
                viewInstance.GiftSentText.text = $"Gift Sent to {inputData.RecipientName}!";

            if (inputData.UserThumbnail != null)
                viewInstance.RecipientThumbnail.sprite = inputData.UserThumbnail;

            AutoCloseAfterDelayAsync(lifeCts.Token).Forget();
        }

        protected override void OnViewClose()
        {
            lifeCts.SafeCancelAndDispose();
        }

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct)
        {
            if (viewInstance == null)
                return UniTask.Never(ct);

            return viewInstance.CloseButton.OnClickAsync(ct);
        }

        private async UniTaskVoid AutoCloseAfterDelayAsync(CancellationToken ct)
        {
            try
            {
                await UniTask.Delay(TimeSpan.FromSeconds(3), cancellationToken: ct);
                SignalClose();
            }
            catch (OperationCanceledException)
            {
                /* swallow */
            }
        }

        private void SignalClose()
        {
            if (viewInstance == null) return;

            if (viewInstance.CloseButton != null && viewInstance.CloseButton.interactable)
                viewInstance.CloseButton.onClick.Invoke();
        }
    }
}