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
        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Popup;

        private const string WAITING_FOR_WALLET_MESSAGE = "A browser window should open for you to confirm the transaction.";
        private const string PREPARING_GIFT_TITLE = "Preparing Gift for";
        private const string DEFAULT_STATUS_MESSAGE = "Processing...";
        private const string ERROR_DIALOG_TITLE = "Something went wrong";
        private const string ERROR_DIALOG_CANCEL_TEXT = "CLOSE";
        private const string ERROR_DIALOG_CONFIRM_TEXT = "TRY AGAIN";
        private const string ERROR_DIALOG_DESCRIPTION = "Your gift wasn't delivered. Please try again or contact Support.";
        private const string ERROR_DIALOG_SUPPORT_LINK_FORMAT = "<link=\"{0}\"><color=#D5A5E2>Contact Support</color></link>";
        private const string RECIPIENT_NAME_RICH_TEXT_FORMAT = "<color=#{0}>{1}</color>";
        private const string RETRY_LOG_MESSAGE = "User clicked RETRY.";

        private enum State { Waiting, Success, Failed }

        private State currentState;

        private readonly IEventBus eventBus;
        private readonly IWebBrowser webBrowser;
        private readonly IMVCManager mvcManager;
        private readonly IDecentralandUrlsSource decentralandUrlsSource;
        private readonly GiftTransferRequestCommand  giftTransferRequestCommand;
        
        private IDisposable? subProgress;
        private IDisposable? subSucceeded;
        private IDisposable? subFailed;
        private IDisposable? subOpenRequested;

        private CancellationTokenSource? lifeCts;
        private CancellationTokenSource? delayCts;

        private string urn = string.Empty;

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

        protected override void OnViewInstantiated()
        {
            if (viewInstance == null) return;
        }

        protected override void OnViewShow()
        {
            lifeCts = new CancellationTokenSource();

            urn = inputData.giftUrn;

            if (viewInstance != null)
            {
                viewInstance.MarketplaceLink.OnLinkClicked += OnMarketplaceActivityLinkClicked;

                viewInstance.RecipientName.text = string.Format(
                    RECIPIENT_NAME_RICH_TEXT_FORMAT,
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
                SetPhase(GiftTransferPhase.WaitingForWallet, WAITING_FOR_WALLET_MESSAGE);
            }

            subProgress = eventBus.Subscribe<GiftTransferProgress>(OnProgress);
            subSucceeded = eventBus.Subscribe<GiftTransferSucceeded>(OnSuccess);
            subFailed = eventBus.Subscribe<GiftTransferFailed>(OnFailure);

            // Start the process
            giftTransferRequestCommand.ExecuteAsync(inputData, lifeCts.Token).Forget();
        }

        private void OnMarketplaceActivityLinkClicked(string _)
        {
            LinkCallback(decentralandUrlsSource.Url(DecentralandUrl.MarketplaceLink));
        }

        protected override void OnViewClose()
        {
            if (viewInstance != null)
                viewInstance.MarketplaceLink.OnLinkClicked -= OnMarketplaceActivityLinkClicked;
            
            subProgress?.Dispose();
            subSucceeded?.Dispose();
            subFailed?.Dispose();
            delayCts.SafeCancelAndDispose();
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
                viewInstance.BackButton.OnClickAsync(ct), viewInstance.CloseButton.OnClickAsync(ct)
            };

            await UniTask.WhenAny(closeTasks);
        }

        private void OnProgress(GiftTransferProgress e)
        {
            SetPhase(e.Phase, e.Message);

            if (e.Phase == GiftTransferPhase.Authorizing)
                StartDelayedStateTimer(lifeCts!.Token);
        }

        private void StartDelayedStateTimer(CancellationToken ct)
        {
            delayCts = delayCts.SafeRestartLinked(ct);
            ShowAfterDelay(delayCts.Token).Forget();
        }

        private async UniTaskVoid ShowAfterDelay(CancellationToken token)
        {
            try
            {
                await UniTask.Delay(TimeSpan.FromSeconds(10), cancellationToken: token);
                if (viewInstance != null && viewInstance.LongRunningHint != null)
                    viewInstance.LongRunningHint.gameObject.SetActive(true);
            }
            catch (OperationCanceledException)
            {
                /* Expected */
            }
        }

        private void OnSuccess(GiftTransferSucceeded e)
        {
            if (e.Urn != urn) return;

            currentState = State.Success;
            delayCts.SafeCancelAndDispose();
            RequestClose();
            OpenSuccessScreenAfterCloseAsync(CancellationToken.None).Forget();
        }

        private async UniTaskVoid OpenSuccessScreenAfterCloseAsync(CancellationToken ct)
        {
            var successParams = new GiftTransferSuccessParams(inputData.recipientName, inputData.userThumbnail, inputData.userNameColorHex);
            await mvcManager.ShowAsync(GiftTransferSuccessController.IssueCommand(successParams), ct);
        }

        private void OnFailure(GiftTransferFailed e)
        {
            if (e.Urn != urn) return;
            currentState = State.Failed;
            delayCts.SafeCancelAndDispose();

            RequestClose();
            ShowErrorPopupAsync(CancellationToken.None).Forget();
        }

        private void SetViewState(State newState)
        {
            currentState = newState;
            if (viewInstance == null) return;

            switch (newState)
            {
                case State.Waiting:
                    viewInstance.TitleLabel.text = PREPARING_GIFT_TITLE;
                    viewInstance.StatusContainer.SetActive(true);

                    if (viewInstance.LongRunningHint != null)
                        viewInstance.LongRunningHint.gameObject.SetActive(false);

                    break;
            }
        }

        private void SetPhase(GiftTransferPhase phase, string? msg)
        {
            if (viewInstance == null || currentState != State.Waiting) return;
            viewInstance.StatusText.text = msg ?? DEFAULT_STATUS_MESSAGE;
        }

        private void RequestClose()
        {
            viewInstance?.CloseButton.onClick.Invoke();
        }

        private async UniTaskVoid ShowErrorPopupAsync(CancellationToken ct)
        {
            string supportUrl = decentralandUrlsSource.Url(DecentralandUrl.Support);
            string supportLink = string.Format(ERROR_DIALOG_SUPPORT_LINK_FORMAT, supportUrl);

            var dialogParams = new ConfirmationDialogParameter(
                ERROR_DIALOG_TITLE,
                ERROR_DIALOG_CANCEL_TEXT,
                ERROR_DIALOG_CONFIRM_TEXT,
                viewInstance?.WarningIcon,
                false,
                false,
                null,
                ERROR_DIALOG_DESCRIPTION,
                linkText: supportLink,
                onLinkClickCallback: LinkCallback
            );

            var result = await ViewDependencies
                .ConfirmationDialogOpener
                .OpenConfirmationDialogAsync(dialogParams, ct);

            if (result == ConfirmationResult.CONFIRM)
            {
                ReportHub.Log(ReportCategory.GIFTING, RETRY_LOG_MESSAGE);
                await mvcManager.ShowAsync(IssueCommand(inputData), ct);
            }
        }

        private void LinkCallback(string url)
        {
            webBrowser.OpenUrl(url);
        }
    }
}