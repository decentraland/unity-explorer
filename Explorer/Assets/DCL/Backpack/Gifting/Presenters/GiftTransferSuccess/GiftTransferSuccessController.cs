using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.Audio;
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
            if (viewInstance.GiftSentText != null)
                viewInstance.GiftSentText.text = $"Gift Sent to {inputData.RecipientName}!";

            if (inputData.UserThumbnail != null)
                viewInstance.RecipientThumbnail.sprite = inputData.UserThumbnail;

            PlayAnimationAndAutoCloseAsync().Forget();
        }

        private async UniTaskVoid PlayAnimationAndAutoCloseAsync()
        {
            lifeCts = new CancellationTokenSource();
            await PlayShowAnimationAsync(lifeCts.Token);
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

        public async UniTask PlayShowAnimationAsync(CancellationToken ct)
        {
            await viewInstance.BackgroundRaysAnimation.ShowAnimationAsync(ct);

            // if (config.Sound != null)
            //     UIAudioEventsBus.Instance.SendPlayAudioEvent(config.Sound);
        }

        public async UniTask PlayHideAnimationAsync(CancellationToken ct)
        {
            await viewInstance.BackgroundRaysAnimation.HideAnimationAsync(ct);
        }

        private async UniTaskVoid AutoCloseAfterDelayAsync(CancellationToken ct)
        {
            try
            {
                await UniTask.Delay(TimeSpan.FromSeconds(3), cancellationToken: ct);
                await PlayHideAnimationAsync(ct);
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