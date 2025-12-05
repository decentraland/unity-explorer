using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.Backpack.Gifting.Presenters.GiftTransfer.Commands;
using DCL.Backpack.Gifting.Views;
using DCL.Browser;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.UI.ConfirmationDialog.Opener;
using MVC;
using Utility;
using static DCL.Backpack.Gifting.Events.GiftingEvents;

namespace DCL.Backpack.Gifting.Presenters
{
    public sealed class GiftTransferController
        : ControllerBase<GiftTransferStatusView, GiftTransferParams>
    {
        private static readonly TimeSpan LONG_RUNNING_HINT_DELAY = TimeSpan.FromSeconds(10);
        
        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Popup;

        private enum State { Waiting, Success, Failed }

        private State currentState;

        private readonly IEventBus eventBus;
        private readonly IWebBrowser webBrowser;
        private readonly IMVCManager mvcManager;
        private readonly IDecentralandUrlsSource decentralandUrlsSource;
        private readonly GiftTransferRequestCommand  giftTransferRequestCommand;

        private IDisposable? subProgress;

        private CancellationTokenSource? lifeCts;
        private CancellationTokenSource? delayCts;

        public GiftTransferController(ViewFactoryMethod viewFactory,
            IWebBrowser webBrowser,
            IEventBus eventBus,
            IMVCManager mvcManager,
            IDecentralandUrlsSource decentralandUrlsSource,
            GiftTransferRequestCommand giftTransferRequestCommand
        )
            : base(viewFactory)
        {
            this.webBrowser = webBrowser;
            this.eventBus = eventBus;
            this.mvcManager = mvcManager;
            this.decentralandUrlsSource = decentralandUrlsSource;
            this.giftTransferRequestCommand = giftTransferRequestCommand;
        }
        
        protected override void OnViewShow()
        {
            lifeCts = new CancellationTokenSource();

            if (viewInstance != null)
            {
                viewInstance.MarketplaceLink.OnLinkClicked += OnMarketplaceActivityLinkClicked;

                viewInstance.RecipientName.text = string.Format(
                    GiftingTextIds.ColoredTextFormat,
                    inputData.userNameColorHex,
                    inputData.recipientName
                );

                if (inputData.userThumbnail != null)
                    viewInstance.RecipientAvatar.sprite = inputData.userThumbnail;

                viewInstance.ItemName.text = inputData.giftDisplayName;

                if (inputData.giftThumbnail != null)
                    viewInstance.ItemThumbnail.sprite = inputData.giftThumbnail;

                viewInstance.ItemCategory.sprite = inputData.style.categoryIcon;
                viewInstance.ItemCategoryBackground.color = inputData.style.flapColor;
                viewInstance.ItemBackground.sprite = inputData.style.rarityBackground;

                SetViewState(State.Waiting);
                SetPhase(GiftTransferPhase.WaitingForWallet, GiftingTextIds.WaitingForWalletMessage);
            }

            subProgress = eventBus.Subscribe<GiftTransferProgress>(OnProgress);

            ProcessTransferFlowAsync(lifeCts.Token)
                .Forget();
        }

        protected override void OnViewClose()
        {
            if (viewInstance != null)
                viewInstance.MarketplaceLink.OnLinkClicked -= OnMarketplaceActivityLinkClicked;

            subProgress?.Dispose();
            delayCts.SafeCancelAndDispose();
            lifeCts.SafeCancelAndDispose();
        }

        protected override async UniTask WaitForCloseIntentAsync(CancellationToken ct)
        {
            await UniTask.WhenAny(viewInstance.BackButton.OnClickAsync(ct),
                viewInstance.CloseButton.OnClickAsync(ct));
        }

        private async UniTask ProcessTransferFlowAsync(CancellationToken ct)
        {
            try
            {
                var result = await giftTransferRequestCommand.ExecuteAsync(inputData, ct);
                ct.ThrowIfCancellationRequested();

                if (result.IsSuccess)
                    OnSuccess();
                else
                    OnFailure();
            }
            catch (OperationCanceledException)
            {
                // expected, ignore
            }
            catch (Exception e)
            {
                ReportHub.LogException(e, new ReportData(ReportCategory.GIFTING));
                OnFailure();
            }
        }

        private void OnMarketplaceActivityLinkClicked(string _)
        {
            LinkCallback(decentralandUrlsSource.Url(DecentralandUrl.MarketplaceLink));
        }

        private void OnProgress(GiftTransferProgress e)
        {
            SetPhase(e.Phase, e.Message);

            if (e.Phase != GiftTransferPhase.Authorizing)
                return;

            if (lifeCts == null || lifeCts.IsCancellationRequested)
                return;

            StartDelayedStateTimer(lifeCts.Token);
        }

        private void StartDelayedStateTimer(CancellationToken ct)
        {
            delayCts = delayCts.SafeRestartLinked(ct);
            ShowAfterDelayAsync(delayCts.Token)
                .Forget();
        }

        private async UniTask ShowAfterDelayAsync(CancellationToken token)
        {
            try
            {
                // If the Web3 signature prompt takes too long,
                // show a hint so users don't think the UI is frozen.
                await UniTask.Delay(LONG_RUNNING_HINT_DELAY, cancellationToken: token);
                
                if (viewInstance != null && viewInstance.LongRunningHint != null)
                    viewInstance.LongRunningHint.gameObject.SetActive(true);
            }
            catch (OperationCanceledException)
            {
                /* Expected */
            }
        }

        private void OnSuccess()
        {
            currentState = State.Success;
            delayCts.SafeCancelAndDispose();
            RequestClose();
            
            OpenSuccessScreenAfterCloseAsync(CancellationToken.None)
                .Forget();
        }

        private void OnFailure()
        {
            currentState = State.Failed;
            delayCts.SafeCancelAndDispose();

            RequestClose();
            
            ShowErrorPopupAsync(CancellationToken.None)
                .Forget();
        }

        private async UniTask OpenSuccessScreenAfterCloseAsync(CancellationToken ct)
        {
            try
            {
                var successParams = new GiftTransferSuccessParams(inputData.recipientName,
                    inputData.userThumbnail,
                    inputData.userNameColorHex);

                await mvcManager.ShowAsync(GiftTransferSuccessController.IssueCommand(successParams), ct);
            }
            catch (OperationCanceledException)
            {
                // ignore, user navigated away
            }
            catch (Exception e)
            {
                ReportHub.LogException(e, new ReportData(ReportCategory.GIFTING));
            }
        }
        
        private void SetViewState(State newState)
        {
            currentState = newState;
            if (viewInstance == null) return;

            switch (newState)
            {
                case State.Waiting:
                    viewInstance.TitleLabel.text = GiftingTextIds.PreparingGiftTitle;
                    viewInstance.StatusContainer.SetActive(true);

                    if (viewInstance.LongRunningHint != null)
                        viewInstance.LongRunningHint.gameObject.SetActive(false);

                    break;
            }
        }

        private void SetPhase(GiftTransferPhase phase, string? msg)
        {
            if (viewInstance == null || currentState != State.Waiting) return;
            viewInstance.StatusText.text = msg ?? GiftingTextIds.DefaultStatusMessage;
        }

        private void RequestClose()
        {
            viewInstance?.CloseButton.onClick.Invoke();
        }

        private async UniTask ShowErrorPopupAsync(CancellationToken ct)
        {
            try
            {
                string supportUrl = decentralandUrlsSource.Url(DecentralandUrl.Support);
                string supportLink = string.Format(GiftingTextIds.ErrorDialogSupportLinkFormat, supportUrl);

                var dialogParams = new ConfirmationDialogParameter(
                    GiftingTextIds.ErrorDialogTitle,
                    GiftingTextIds.ErrorDialogCancelText,
                    GiftingTextIds.ErrorDialogConfirmText,
                    viewInstance?.WarningIcon,
                    false,
                    false,
                    null,
                    GiftingTextIds.ErrorDialogDescription,
                    linkText: supportLink,
                    onLinkClickCallback: LinkCallback
                );

                var result = await ViewDependencies
                    .ConfirmationDialogOpener
                    .OpenConfirmationDialogAsync(dialogParams, ct);

                if (result == ConfirmationResult.CONFIRM)
                {
                    ReportHub.Log(ReportCategory.GIFTING, GiftingTextIds.RetryLogMessage);
                    await mvcManager.ShowAsync(IssueCommand(inputData), ct);
                }
            }
            catch (OperationCanceledException)
            {
                // dialog closed or navigation away
            }
            catch (Exception e)
            {
                ReportHub.LogException(e, new ReportData(ReportCategory.GIFTING));
            }
        }

        private void LinkCallback(string url)
        {
            webBrowser.OpenUrl(url);
        }
    }
}