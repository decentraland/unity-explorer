using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.Audio;
using DCL.Backpack.Gifting.Views;
using DCL.Diagnostics;
using DCL.Utilities.Extensions;
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

        protected override void OnViewShow()
        {
            if (viewInstance != null)
            {
                viewInstance.GiftSentText.text = string.Format(
                    GiftingTextIds.GiftSentTextFormat,
                    inputData.UserNameColorHex,
                    inputData.RecipientName
                );

                if (inputData.UserThumbnail != null)
                    viewInstance.RecipientThumbnail.sprite = inputData.UserThumbnail;
            }

            lifeCts = new CancellationTokenSource();

            PlayAnimationAsync(lifeCts.Token)
                .Forget();
        }

        private async UniTask PlayAnimationAsync(CancellationToken ct)
        {
            await PlayShowAnimationAsync(ct)
                .SuppressToResultAsync(ReportCategory.GIFTING);
        }

        protected override void OnViewClose()
        {
            lifeCts.SafeCancelAndDispose();
        }

        protected override async UniTask WaitForCloseIntentAsync(CancellationToken ct)
        {
            await UniTask.WhenAny( viewInstance.OkButton.OnClickAsync(ct), viewInstance.CloseButton.OnClickAsync(ct));
            await PlayHideAnimationAsync(CancellationToken.None);
        }

        private async UniTask PlayShowAnimationAsync(CancellationToken ct)
        {
            if (viewInstance !=  null)
            {
                await viewInstance.BackgroundRaysAnimation.ShowAnimationAsync(ct);

                if (viewInstance.Sound != null)
                    UIAudioEventsBus.Instance.SendPlayAudioEvent(viewInstance.Sound);
            }
        }

        private async UniTask PlayHideAnimationAsync(CancellationToken ct)
        {
            if (viewInstance != null)
                await viewInstance.BackgroundRaysAnimation.HideAnimationAsync(ct);
        }
    }
}