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

        private enum State { Waiting, Success, Failed }

        private State currentState;

        private readonly IEventBus eventBus;
        private readonly IWebBrowser webBrowser;
        private readonly IMVCManager mvcManager;
        private readonly IDecentralandUrlsSource decentralandUrlsSource;
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
            IWebBrowser webBrowser,
            IEventBus eventBus,
            IMVCManager mvcManager,
            IDecentralandUrlsSource decentralandUrlsSource,
            GiftTransferProgressCommand giftTransferProgressCommand,
            GiftTransferRequestCommand giftTransferRequestCommand,
            GiftTransferResponseCommand  giftTransferResponseCommand,
            GiftTransferSignCommand  giftTransferSignCommand
        )
            : base(viewFactory)
        {
            this.webBrowser = webBrowser;
            this.eventBus = eventBus;
            this.mvcManager = mvcManager;
            this.decentralandUrlsSource = decentralandUrlsSource;
            this.giftTransferProgressCommand = giftTransferProgressCommand;
            this.giftTransferRequestCommand = giftTransferRequestCommand;
            this.giftTransferResponseCommand = giftTransferResponseCommand;
            this.giftTransferSignCommand = giftTransferSignCommand;
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
                viewInstance.RecipientName.text = $"<color=#{inputData.userNameColorHex}>{inputData.recipientName}</color>";

                if (inputData.userThumbnail != null)
                    viewInstance.RecipientAvatar.sprite = inputData.userThumbnail;

                viewInstance.ItemName.text = inputData.giftDisplayName;

                if (inputData.giftThumbnail != null)
                    viewInstance.ItemThumbnail.sprite = inputData.giftThumbnail;

                viewInstance.ItemCategory.sprite = inputData.style.categoryIcon;
                viewInstance.ItemCategoryBackground.color = inputData.style.flapColor;
                viewInstance.ItemBackground.sprite = inputData.style.rarityBackground;

                SetViewState(State.Waiting);
                SetPhase(GiftTransferPhase.WaitingForWallet, "A browser window should open for you to confirm the transaction.");
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
                    viewInstance.TitleLabel.text = "Preparing Gift for";
                    viewInstance.StatusContainer.SetActive(true);

                    if (viewInstance.LongRunningHint != null)
                        viewInstance.LongRunningHint.gameObject.SetActive(false);

                    break;
            }
        }

        private void SetPhase(GiftTransferPhase phase, string? msg)
        {
            if (viewInstance == null || currentState != State.Waiting) return;
            viewInstance.StatusText.text = msg ?? "Processing...";
        }

        private void RequestClose()
        {
            viewInstance?.CloseButton.onClick.Invoke();
        }

        private async UniTaskVoid ShowErrorPopupAsync(CancellationToken ct)
        {
            var dialogParams = new ConfirmationDialogParameter(
                "Something went wrong",
                "CLOSE",
                "TRY AGAIN",
                viewInstance?.WarningIcon,
                false,
                false,
                null,
                "Your gift wasn't delivered. Please try again of contact Support.",
                linkText: $"<link=\"{decentralandUrlsSource.Url(DecentralandUrl.Support)}\"><color=#D5A5E2>Contact Support</color></link>",
                onLinkClickCallback: LinkCallback
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

        private void LinkCallback(string url)
        {
            webBrowser.OpenUrl(url);
        }
    }
}