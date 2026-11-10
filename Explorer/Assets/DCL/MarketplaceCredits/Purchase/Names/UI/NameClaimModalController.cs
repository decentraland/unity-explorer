using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Web3.Identities;
using MVC;
using Plugins.NativeWindowManager;
using System;
using System.Threading;
using Utility;

namespace DCL.MarketplaceCredits.Purchase.Names.UI
{
    /// <summary>
    ///     Confirms a NAME claim: shows the credits price against the balance, runs the claim and reports how
    ///     it ended. Closing the modal while the bridge is registering does not stop the claim.
    /// </summary>
    public class NameClaimModalController : ControllerBase<NameClaimModalView, NameClaimModalParams>
    {
        private enum ModalState
        {
            Loading,
            ReadyToConfirm,
            InsufficientCredits,
            Claiming,
            Result,
        }

        private const string CANNOT_AFFORD_TEXT = "Add <b>{0} Credits</b> to claim this NAME.";
        private const string SUCCESS_TITLE = "NAME claimed!";
        private const string PENDING_TITLE = "Registration in progress";
        private const string FAILED_TITLE = "The NAME was not claimed";
        private const string SUCCESS_TEXT = "<b>{0}</b> is yours. You can set it as your name from your profile.";
        private const string PENDING_TEXT = "Your payment was sent and <b>{0}</b> is still being registered. It can take a few minutes: check your names later.";

        private readonly NameClaimService claimService;
        private readonly MarketplaceCreditsAPIClient creditsApiClient;
        private readonly IWeb3IdentityCache identityCache;
        private readonly Func<CancellationToken, UniTask> openGetCreditsPanelAsync;
        private readonly CancellationTokenSource disposalCts = new ();

        private ModalState currentState;
        private CancellationTokenSource? lifeCts;
        private NameClaimQuote quote;

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Popup;

        public NameClaimModalController(
            ViewFactoryMethod viewFactory,
            NameClaimService claimService,
            MarketplaceCreditsAPIClient creditsApiClient,
            IWeb3IdentityCache identityCache,
            Func<CancellationToken, UniTask> openGetCreditsPanelAsync) : base(viewFactory)
        {
            this.claimService = claimService;
            this.creditsApiClient = creditsApiClient;
            this.identityCache = identityCache;
            this.openGetCreditsPanelAsync = openGetCreditsPanelAsync;
            claimService.StageChanged += OnClaimStageChanged;
        }

        public override void Dispose()
        {
            claimService.StageChanged -= OnClaimStageChanged;
            lifeCts.SafeCancelAndDispose();
            disposalCts.SafeCancelAndDispose();
        }

        protected override void OnViewShow()
        {
            lifeCts = new CancellationTokenSource();

            if (viewInstance != null)
            {
                viewInstance.NameText.text = inputData.Name;
                viewInstance.ConfirmButton.onClick.AddListener(OnConfirmClicked);
                viewInstance.RetryButton.onClick.AddListener(OnRetryClicked);
                viewInstance.GetCreditsButton.onClick.AddListener(OnGetCreditsClicked);
            }

            LoadQuoteAndBalanceAsync(lifeCts.Token).Forget();
        }

        protected override void OnViewClose()
        {
            if (viewInstance != null)
            {
                viewInstance.ConfirmButton.onClick.RemoveListener(OnConfirmClicked);
                viewInstance.RetryButton.onClick.RemoveListener(OnRetryClicked);
                viewInstance.GetCreditsButton.onClick.RemoveListener(OnGetCreditsClicked);
            }

            lifeCts.SafeCancelAndDispose();
            lifeCts = null;
        }

        protected override async UniTask WaitForCloseIntentAsync(CancellationToken ct)
        {
            if (viewInstance == null)
                return;

            await UniTask.WhenAny(
                viewInstance.CloseButton.OnClickAsync(ct),
                viewInstance.CancelButton.OnClickAsync(ct),
                viewInstance.DoneButton.OnClickAsync(ct),
                viewInstance.CloseBackground.OnClickAsync(ct));
        }

        private async UniTaskVoid LoadQuoteAndBalanceAsync(CancellationToken ct)
        {
            SetUiState(ModalState.Loading);

            if (viewInstance != null)
            {
                viewInstance.PriceCreditsText.text = string.Empty;
                viewInstance.BalanceCreditsText.text = string.Empty;
                viewInstance.PriceLoadingSpinner.SetActive(true);
            }

            IWeb3Identity? identity = identityCache.Identity;

            if (identity == null)
            {
                ShowResult(FAILED_TITLE, "You need to be signed in to claim a NAME.", allowRetry: false);
                return;
            }

            NameQuoteResult quoteResult = await claimService.QuoteAsync(ct);

            if (ct.IsCancellationRequested)
                return;

            if (!quoteResult.Success)
            {
                ShowError(quoteResult.Error);
                return;
            }

            quote = quoteResult.Quote;

            if (viewInstance != null)
            {
                viewInstance.PriceLoadingSpinner.SetActive(false);

                // The price is 100 MANA converted at the live rate, so it floats.
                viewInstance.PriceCreditsText.text = $"≈{quote.Credits}";
            }

            try
            {
                UserCreditsResponse credits = await creditsApiClient.GetUserCreditsAsync(identity.Address, ct);

                if (ct.IsCancellationRequested)
                    return;

                bool canAfford = credits.usd.credits >= quote.Credits;

                if (viewInstance != null)
                {
                    viewInstance.BalanceCreditsText.text = credits.usd.credits.ToString();
                    viewInstance.CannotAffordText.text = string.Format(CANNOT_AFFORD_TEXT, quote.Credits - credits.usd.credits);
                }

                SetUiState(canAfford ? ModalState.ReadyToConfirm : ModalState.InsufficientCredits);
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                ReportHub.LogException(e, new ReportData(ReportCategory.CREDITS_PURCHASE));
                ShowResult(FAILED_TITLE, "Could not load your credits balance.", allowRetry: true);
            }
        }

        private void OnConfirmClicked()
        {
            if (currentState != ModalState.ReadyToConfirm || lifeCts == null)
                return;

            ClaimAsync(lifeCts.Token).Forget();
        }

        private void OnRetryClicked()
        {
            if (currentState != ModalState.Result || lifeCts == null)
                return;

            LoadQuoteAndBalanceAsync(lifeCts.Token).Forget();
        }

        private void OnGetCreditsClicked()
        {
            viewInstance?.CloseButton.onClick.Invoke();
            OpenGetCreditsAfterCloseAsync(disposalCts.Token).Forget();
        }

        private async UniTaskVoid OpenGetCreditsAfterCloseAsync(CancellationToken ct)
        {
            try { await openGetCreditsPanelAsync(ct); }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                ReportHub.LogException(e, new ReportData(ReportCategory.CREDITS_PURCHASE));
            }
        }

        private async UniTaskVoid ClaimAsync(CancellationToken ct)
        {
            SetUiState(ModalState.Claiming);
            NativeWindowManager.RequestTemporaryWindowMode();

            NameClaimResult result;

            try { result = await claimService.ClaimAsync(inputData.Name, quote, ct); }
            finally { NativeWindowManager.ReleaseTemporaryWindowMode(); }

            if (ct.IsCancellationRequested)
                return;

            switch (result.Status)
            {
                case NameClaimStatus.Registered:
                    ShowResult(SUCCESS_TITLE, string.Format(SUCCESS_TEXT, inputData.Name), allowRetry: false);
                    RefreshBalanceAsync(ct).Forget();
                    break;
                case NameClaimStatus.Pending:
                    ShowResult(PENDING_TITLE, string.Format(PENDING_TEXT, inputData.Name), allowRetry: false);
                    break;
                default:
                    ShowError(result.Error);
                    break;
            }
        }

        private async UniTaskVoid RefreshBalanceAsync(CancellationToken ct)
        {
            IWeb3Identity? identity = identityCache.Identity;

            if (identity == null)
                return;

            try
            {
                UserCreditsResponse credits = await creditsApiClient.GetUserCreditsAsync(identity.Address, ct);

                if (!ct.IsCancellationRequested && viewInstance != null)
                    viewInstance.BalanceCreditsText.text = credits.usd.credits.ToString();
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                ReportHub.LogWarning(ReportCategory.CREDITS_PURCHASE, $"Balance refresh after a NAME claim failed: {e.Message}");
            }
        }

        // Retry is offered only for failures that provably never spent the credits.
        private void ShowError(NameClaimError error)
        {
            switch (error)
            {
                case NameClaimError.FeatureDisabled:
                    ShowResult(FAILED_TITLE, "Claiming NAMEs with credits is not available right now.", allowRetry: false);
                    break;
                case NameClaimError.NameUnavailable:
                    ShowResult(FAILED_TITLE, "This NAME is no longer available. Choose another one.", allowRetry: false);
                    break;
                case NameClaimError.PriceUnavailable:
                    ShowResult(FAILED_TITLE, "The price could not be loaded. Please try again.", allowRetry: true);
                    break;
                case NameClaimError.RouteTooExpensive:
                    ShowResult(FAILED_TITLE, "Registration is too expensive right now. Please try again later.", allowRetry: true);
                    break;
                case NameClaimError.InsufficientCredits:
                    ShowResult(FAILED_TITLE, "You do not have enough credits to claim this NAME.", allowRetry: true);
                    break;
                case NameClaimError.PriceChanged:
                    ShowResult(FAILED_TITLE, "The price changed. Review it and try again.", allowRetry: true);
                    break;
                case NameClaimError.SignatureRejected:
                    ShowResult(FAILED_TITLE, "The signature was rejected. Your credits were not spent.", allowRetry: true);
                    break;
                case NameClaimError.TransactionReverted:
                    ShowResult(FAILED_TITLE, "The payment failed on-chain. Your credits were not spent.", allowRetry: true);
                    break;
                case NameClaimError.NotRegistered:
                    ShowResult(FAILED_TITLE, "The payment went through but the NAME could not be registered. Please contact support.", allowRetry: false);
                    break;
                case NameClaimError.Cancelled:
                    SetUiState(ModalState.ReadyToConfirm);
                    break;
                default:
                    ShowResult(FAILED_TITLE, "The claim failed. Your credits were not spent.", allowRetry: true);
                    break;
            }
        }

        private void OnClaimStageChanged(NameClaimStage stage)
        {
            if (viewInstance == null || currentState != ModalState.Claiming)
                return;

            viewInstance.ProgressStatusText.text = stage switch
                                                   {
                                                       NameClaimStage.Preparing => "Reserving your credits...",
                                                       NameClaimStage.Signing => "Waiting for your signature...",
                                                       NameClaimStage.Confirming => "Confirming the payment...",
                                                       NameClaimStage.Registering => "Registering your NAME. This can take a few minutes...",
                                                       _ => viewInstance.ProgressStatusText.text,
                                                   };
        }

        private void ShowResult(string title, string text, bool allowRetry)
        {
            SetUiState(ModalState.Result);

            if (viewInstance == null)
                return;

            viewInstance.PriceLoadingSpinner.SetActive(false);
            viewInstance.ResultTitleText.text = title;
            viewInstance.ResultText.text = text;
            viewInstance.RetryButton.gameObject.SetActive(allowRetry);
        }

        private void SetUiState(ModalState newState)
        {
            currentState = newState;

            if (viewInstance == null)
                return;

            viewInstance.ConfirmStateContainer.SetActive(newState is ModalState.Loading or ModalState.ReadyToConfirm);
            viewInstance.ConfirmButton.interactable = newState == ModalState.ReadyToConfirm;
            viewInstance.InsufficientCreditsContainer.SetActive(newState == ModalState.InsufficientCredits);
            viewInstance.ProgressStateContainer.SetActive(newState == ModalState.Claiming);
            viewInstance.ResultStateContainer.SetActive(newState == ModalState.Result);
        }
    }
}
