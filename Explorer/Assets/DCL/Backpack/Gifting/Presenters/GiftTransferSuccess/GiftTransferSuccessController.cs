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
            if (viewInstance.GiftSentText != null)
                viewInstance.GiftSentText.text = $"Gift Sent to {inputData.RecipientName}!";

            if (inputData.UserThumbnail != null)
                viewInstance.RecipientThumbnail.sprite = inputData.UserThumbnail;

            PlayAnimationAsync().Forget();
        }

        private async UniTaskVoid PlayAnimationAsync()
        {
            lifeCts = new CancellationTokenSource();
            await PlayShowAnimationAsync(lifeCts.Token);
        }

        protected override void OnViewClose()
        {
            lifeCts.SafeCancelAndDispose();
        }

        protected override async UniTask WaitForCloseIntentAsync(CancellationToken ct)
        {
            if (viewInstance == null)
            {
                await UniTask.Never(ct);
                return;
            }

            var closeTasks = new[]
            {
                viewInstance.OkButton.OnClickAsync(ct), viewInstance.CloseButton.OnClickAsync(ct)
            };

            await UniTask.WhenAny(closeTasks);

            await PlayHideAnimationAsync(ct);
        }

        private async UniTask PlayShowAnimationAsync(CancellationToken ct)
        {
            if (viewInstance !=  null)
            {
                await viewInstance.BackgroundRaysAnimation.ShowAnimationAsync(ct);

                // if (config.Sound != null)
                //     UIAudioEventsBus.Instance.SendPlayAudioEvent(config.Sound);
            }
        }

        private async UniTask PlayHideAnimationAsync(CancellationToken ct)
        {
            if (viewInstance != null)
                await viewInstance.BackgroundRaysAnimation.HideAnimationAsync(ct);
        }
    }
}