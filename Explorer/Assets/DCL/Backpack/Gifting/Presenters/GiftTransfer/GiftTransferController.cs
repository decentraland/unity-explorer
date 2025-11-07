using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.Backpack.Gifting.Presenters.GiftTransfer.Commands;
using DCL.Backpack.Gifting.Views;
using MVC;
using Utility;
using static DCL.Backpack.Gifting.Events.GiftingEvents;

namespace DCL.Backpack.Gifting.Presenters
{
    public sealed class GiftTransferController
        : ControllerBase<GiftTransferStatusView, GiftTransferStatusParams>
    {
        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Popup;

        private readonly IEventBus eventBus;
        private readonly GiftTransferProgressCommand giftTransferProgressCommand;
        private readonly GiftTransferRequestCommand  giftTransferRequestCommand;
        private readonly GiftTransferResponseCommand  giftTransferResponseCommand;
        private readonly GiftTransferSignCommand  giftTransferSignCommand;

        // Event subscriptions with meaningful names
        private IDisposable? subProgress;
        private IDisposable? subSucceeded;
        private IDisposable? subFailed;
        private IDisposable? subOpenRequested;

        private CancellationTokenSource? lifeCts;
        private string urn = string.Empty;

        public GiftTransferController(ViewFactoryMethod viewFactory, IEventBus eventBus,
            GiftTransferProgressCommand giftTransferProgressCommand,
            GiftTransferRequestCommand giftTransferRequestCommand,
            GiftTransferResponseCommand  giftTransferResponseCommand,
            GiftTransferSignCommand  giftTransferSignCommand
        )
            : base(viewFactory)
        {
            this.eventBus = eventBus;
            this.giftTransferProgressCommand = giftTransferProgressCommand;
            this.giftTransferRequestCommand = giftTransferRequestCommand;
            this.giftTransferResponseCommand = giftTransferResponseCommand;
            this.giftTransferSignCommand = giftTransferSignCommand;
        }

        protected override void OnViewInstantiated()
        {
            // Wire UI -> controller intents here (static, once)
            if (viewInstance == null) return;
        }

        protected override void OnViewShow()
        {
            lifeCts = new CancellationTokenSource();

            urn = inputData.giftUrn;

            if (viewInstance != null)
            {
                viewInstance.TitleLabel.text = "Preparing Gift for";
                viewInstance.RecipientName.text = inputData.recipientName;
                if (inputData.userThumbnail != null)
                    viewInstance.RecipientAvatar.sprite = inputData.userThumbnail;

                viewInstance.ItemName.text = inputData.giftDisplayName;
                if (inputData.giftThumbnail != null)
                    viewInstance.ItemThumbnail.sprite = inputData.giftThumbnail;

                viewInstance.ItemCategory.sprite = inputData.style.categoryIcon;
                viewInstance.ItemCategoryBackground.color = inputData.style.flapColor;
                viewInstance.ItemBackground.sprite = inputData.style.rarityBackground;

                SetPhase(GiftTransferPhase.WaitingForWallet, "A browser window should open for you to confirm the transaction.");
            }

            subProgress = eventBus.Subscribe<GiftTransferProgress>(OnProgress);
            subSucceeded = eventBus.Subscribe<GiftTransferSucceeded>(OnSuccess);
            subFailed = eventBus.Subscribe<GiftTransferFailed>(OnFailure);
            subOpenRequested = eventBus.Subscribe<GiftTransferOpenRequested>(OnOpenRequested);

            giftTransferRequestCommand.ExecuteAsync(inputData, lifeCts.Token).Forget();
        }

        protected override void OnViewClose()
        {
            subProgress?.Dispose();
            subProgress = null;
            subSucceeded?.Dispose();
            subSucceeded = null;
            subFailed?.Dispose();
            subFailed = null;
            subOpenRequested?.Dispose();
            subOpenRequested = null;

            lifeCts.SafeCancelAndDispose();
        }

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct)
        {
            if (viewInstance == null) return UniTask.Never(ct);

            var closeClick = viewInstance.CloseButton.OnClickAsync(ct);
            if (viewInstance.BackButton != null)
            {
                var backClick  = viewInstance.BackButton.OnClickAsync(ct);
                return UniTask.WhenAny(closeClick, backClick).AsUniTask();
            }

            return UniTask.WhenAny(closeClick).AsUniTask();
        }

        private void OnOpenRequested(GiftTransferOpenRequested e)
        {
            if (e.Urn != urn || viewInstance == null) return;

            if (!string.IsNullOrEmpty(e.RecipientName))
                viewInstance.RecipientName.text = e.RecipientName!;
            if (!string.IsNullOrEmpty(e.DisplayName))
                viewInstance.ItemName.text = e.DisplayName!;
            if (e.Thumbnail != null)
                viewInstance.ItemThumbnail.sprite = e.Thumbnail;
        }

        private void OnProgress(GiftTransferProgress e)
        {
            if (e.Urn != urn) return;
            SetPhase(e.Phase, e.Message);
        }

        private void OnSuccess(GiftTransferSucceeded e)
        {
            if (e.Urn != urn) return;
            SetPhase(GiftTransferPhase.Completed, "Gift successfully sent");
            AutoCloseIn(1200).Forget();
        }

        private void OnFailure(GiftTransferFailed e)
        {
            if (e.Urn != urn) return;
            SetPhase(GiftTransferPhase.Failed, e.Reason ?? "Something went wrong");
        }

        private void SetPhase(GiftTransferPhase phase, string? msg)
        {
            if (viewInstance == null) return;

            switch (phase)
            {
                case GiftTransferPhase.WaitingForWallet:
                    viewInstance.StatusText.text = msg ?? "Waiting for wallet…";
                    viewInstance.LongRunningHint?.gameObject.SetActive(false);
                    break;

                case GiftTransferPhase.Authorizing:
                    viewInstance.StatusText.text = msg ?? "Processing authorization…";
                    break;

                case GiftTransferPhase.Broadcasting:
                    viewInstance.StatusText.text = msg ?? "Broadcasting transaction…";
                    break;

                case GiftTransferPhase.Confirming:
                    viewInstance.StatusText.text = msg ?? "Waiting for confirmations…";
                    break;

                case GiftTransferPhase.Completed:
                    viewInstance.StatusText.text = msg ?? "Completed";
                    break;

                case GiftTransferPhase.Failed:
                    viewInstance.StatusText.text = msg ?? "Failed";
                    break;
            }

            // bool showHint = phase == GiftTransferPhase.Authorizing
            //                 || phase == GiftTransferPhase.Broadcasting
            //                 || phase == GiftTransferPhase.Confirming;
            //
            // if (viewInstance.LongRunningHint != null)
            //     viewInstance.LongRunningHint.SetActive(showHint);
        }

        private void RequestClose()
        {
            SignalClose();
        }

        private void SignalClose()
        {
            if (viewInstance == null) return;

            // Prefer CloseButton, fallback to BackButton if Close is absent/disabled
            if (viewInstance.CloseButton != null && viewInstance.CloseButton.interactable && viewInstance.CloseButton.gameObject.activeInHierarchy)
            {
                viewInstance.CloseButton.onClick.Invoke();
                return;
            }

            if (viewInstance.BackButton != null && viewInstance.BackButton.interactable && viewInstance.BackButton.gameObject.activeInHierarchy)
            {
                viewInstance.BackButton.onClick.Invoke();
            }
        }

        private async UniTaskVoid AutoCloseIn(int ms)
        {
            try
            {
                await UniTask.Delay(ms, cancellationToken: lifeCts!.Token);
                SignalClose();
            }
            catch (OperationCanceledException)
            {
                /* ignore */
            }
        }
    }
}