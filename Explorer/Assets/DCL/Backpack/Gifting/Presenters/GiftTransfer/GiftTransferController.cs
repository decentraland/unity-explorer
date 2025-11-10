using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.Backpack.Gifting.Presenters.GiftTransfer.Commands;
using DCL.Backpack.Gifting.Views;
using DCL.Diagnostics;
using DCL.UI.ConfirmationDialog.Opener;
using MVC;
using Utility;
using static DCL.Backpack.Gifting.Events.GiftingEvents;

namespace DCL.Backpack.Gifting.Presenters
{
    public sealed class GiftTransferController
        : ControllerBase<GiftTransferStatusView, GiftTransferParams>
    {
        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Popup;

        private readonly IEventBus eventBus;
        private readonly IMVCManager mvcManager;
        private readonly GiftTransferProgressCommand giftTransferProgressCommand;
        private readonly GiftTransferRequestCommand  giftTransferRequestCommand;
        private readonly GiftTransferResponseCommand  giftTransferResponseCommand;
        private readonly GiftTransferSignCommand  giftTransferSignCommand;
        
        private IDisposable? subProgress;
        private IDisposable? subSucceeded;
        private IDisposable? subFailed;
        private IDisposable? subOpenRequested;

        private CancellationTokenSource? lifeCts;
        private CancellationTokenSource? delayCts;
        
        private string urn = string.Empty;

        public GiftTransferController(ViewFactoryMethod viewFactory,
            IEventBus eventBus,
            IMVCManager mvcManager,
            GiftTransferProgressCommand giftTransferProgressCommand,
            GiftTransferRequestCommand giftTransferRequestCommand,
            GiftTransferResponseCommand  giftTransferResponseCommand,
            GiftTransferSignCommand  giftTransferSignCommand
        )
            : base(viewFactory)
        {
            this.eventBus = eventBus;
            this.mvcManager = mvcManager;
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

            delayCts.SafeCancelAndDispose();
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

            // When we enter the main "waiting" phase, start a timer.
            if (e.Phase == GiftTransferPhase.Authorizing)
            {
                StartDelayedStateTimer(lifeCts!.Token);
            }
        }

        private void StartDelayedStateTimer(CancellationToken ct)
        {
            delayCts.SafeCancelAndDispose();
            delayCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

            async UniTaskVoid ShowAfterDelay(CancellationToken token)
            {
                try
                {
                    // Wait for 10 seconds (or any duration you prefer)
                    await UniTask.Delay(TimeSpan.FromSeconds(10), cancellationToken: token);

                    // If we get here, it means the process is taking too long.
                    // Show the "delayed" UI elements.
                    if (viewInstance != null)
                    {
                        viewInstance.TitleLabel.text = "Authorisation Delayed";
                        viewInstance.LongRunningHint?.gameObject.SetActive(true);
                        // The main close button should also become visible/interactive here
                        viewInstance.CloseButton.gameObject.SetActive(true);
                    }
                }
                catch (OperationCanceledException)
                {
                    /* This is expected if the process succeeds or fails before the delay is over. */
                }
            }

            ShowAfterDelay(delayCts.Token)
                .Forget();
        }

        private async void OnSuccess(GiftTransferSucceeded e)
        {
            if (e.Urn != urn) return;

            delayCts.SafeCancelAndDispose();

            RequestClose();

            var successParams = new GiftTransferSuccessParams(inputData.recipientName,
                inputData.userThumbnail);

            await mvcManager
                .ShowAsync(GiftTransferSuccessController.IssueCommand(successParams));
        }

        private void OnFailure(GiftTransferFailed e)
        {
            if (e.Urn != urn) return;

            delayCts.SafeCancelAndDispose();

            RequestClose();

            ShowErrorPopupAsync(CancellationToken.None)
                .Forget();
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

        private async UniTaskVoid ShowErrorPopupAsync(CancellationToken ct)
        {
            var dialogParams = new ConfirmationDialogParameter(
                "Something went wrong",
                "CLOSE",
                "RETRY",
                null,
                false,
                false,
                null,
                "The Gift could not be delivered. Please retry, and if the problem persist contact Support."
            );

            var result = await ViewDependencies
                .ConfirmationDialogOpener
                .OpenConfirmationDialogAsync(dialogParams, ct);

            if (result == ConfirmationResult.CONFIRM)
            {
                ReportHub.Log(ReportCategory.GIFTING, "User clicked RETRY.");
                await mvcManager.ShowAsync(IssueCommand(inputData), ct);
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