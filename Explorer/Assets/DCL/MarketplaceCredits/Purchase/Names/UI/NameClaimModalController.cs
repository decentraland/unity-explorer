using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Web3.Identities;
using MVC;
using Plugins.NativeWindowManager;
using System;
using System.Threading;
using TMPro;
using Utility;

namespace DCL.MarketplaceCredits.Purchase.Names.UI
{
    /// <summary>
    ///     Confirms a NAME claim: shows the price against what the buyer holds, takes the payment they tick
    ///     (credits, or credits topped up with MANA), runs the claim and reports how it ended. Closing the modal
    ///     once the payment is signed does not stop the claim: reopening it for the same NAME shows the progress
    ///     and then the result.
    /// </summary>
    public class NameClaimModalController : ControllerBase<NameClaimModalView, NameClaimModalParams>
    {
        private enum ModalState
        {
            Loading,
            ReadyToConfirm,
            InsufficientFunds,
            Claiming,
            Success,
            Result,
        }

        private const string BUY_TITLE = "Buy Name";
        private const string INSUFFICIENT_FUNDS_TITLE = "Insufficient Funds";
        private const string MISSING_CREDITS_TEXT = "You will need to buy <b>{0} Credits</b> to purchase this NAME.";
        private const string INSUFFICIENT_FUNDS_TEXT = "Your Credits and MANA are not enough for this NAME. Buy Credits or add MANA to continue.";
        private const string MANA_ALONE_REFUSED_TEXT = "A NAME needs at least 1 Credit: MANA can only cover the rest. Buy Credits to continue.";
        private const string MANA_ALONE_REFUSED_NOTE = "A NAME cannot be paid with MANA alone";
        private const string PENDING_TITLE = "Registration in progress";
        private const string FAILED_TITLE = "The NAME was not claimed";
        private const string SUCCESS_TEXT = "<b>{0}</b> is yours. You can set it as your name from your profile.";
        private const string CLAIM_IN_FLIGHT_TEXT = "Another NAME is still being claimed. Try again when it finishes.";
        private const string PENDING_TEXT = "Your payment was sent and <b>{0}</b> is still being registered. It can take a few minutes: check your names later.";

        private readonly NameClaimService claimService;
        private readonly IWeb3IdentityCache identityCache;
        private readonly Func<CancellationToken, UniTask> openGetCreditsPanelAsync;
        private readonly bool isManaEnabled;
        private readonly CancellationTokenSource disposalCts = new ();

        private ModalState currentState;
        private CancellationTokenSource? lifeCts;
        private NameClaimQuote quote;

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Popup;

        public NameClaimModalController(
            ViewFactoryMethod viewFactory,
            NameClaimService claimService,
            IWeb3IdentityCache identityCache,
            Func<CancellationToken, UniTask> openGetCreditsPanelAsync,
            bool isManaEnabled) : base(viewFactory)
        {
            this.claimService = claimService;
            this.identityCache = identityCache;
            this.openGetCreditsPanelAsync = openGetCreditsPanelAsync;
            this.isManaEnabled = isManaEnabled;
            claimService.StageChanged += OnClaimStageChanged;
        }

        public override void Dispose()
        {
            claimService.StageChanged -= OnClaimStageChanged;
            claimService.Completed -= OnClaimCompleted;
            lifeCts.SafeCancelAndDispose();
            disposalCts.SafeCancelAndDispose();
        }

        protected override void OnViewShow()
        {
            lifeCts = new CancellationTokenSource();

            if (viewInstance != null)
            {
                foreach (TMP_Text nameText in viewInstance.NameTexts)
                    nameText.text = inputData.Name;

                viewInstance.BalanceManaGroup.SetActive(isManaEnabled);
                viewInstance.ConfirmButton.onClick.AddListener(OnConfirmClicked);
                viewInstance.RetryButton.onClick.AddListener(OnRetryClicked);
                viewInstance.GetCreditsButton.onClick.AddListener(OnGetCreditsClicked);
            }

            claimService.Completed += OnClaimCompleted;

            if (claimService.InFlightName == inputData.Name)
            {
                SetUiState(ModalState.Claiming);
                OnClaimStageChanged(claimService.CurrentStage);
            }
            else if (claimService.LastResultName == inputData.Name)
            {
                NameClaimResult finished = claimService.LastResult;
                claimService.AcknowledgeResult();
                ShowClaimResult(finished);
            }
            else
                LoadQuoteAndOptionsAsync(lifeCts.Token).Forget();
        }

        protected override void OnViewClose()
        {
            if (viewInstance != null)
            {
                viewInstance.ConfirmButton.onClick.RemoveListener(OnConfirmClicked);
                viewInstance.RetryButton.onClick.RemoveListener(OnRetryClicked);
                viewInstance.GetCreditsButton.onClick.RemoveListener(OnGetCreditsClicked);
            }

            claimService.Completed -= OnClaimCompleted;
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
                viewInstance.ContinueShoppingButton.OnClickAsync(ct),
                viewInstance.DoneButton.OnClickAsync(ct),
                viewInstance.CloseBackground.OnClickAsync(ct));
        }

        private async UniTaskVoid LoadQuoteAndOptionsAsync(CancellationToken ct)
        {
            SetUiState(ModalState.Loading);

            if (viewInstance != null)
            {
                viewInstance.PriceCreditsText.text = string.Empty;
                viewInstance.BalanceCreditsText.text = PaymentAmountFormat.UNKNOWN;
                viewInstance.BalanceManaText.text = PaymentAmountFormat.UNKNOWN;
                viewInstance.PriceLoadingSpinner.SetActive(true);
                viewInstance.PaymentSelector.Clear(isManaEnabled);
            }

            if (identityCache.Identity == null)
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

            PaymentOptions options = await claimService.GetPaymentOptionsAsync(quote, ct);

            if (ct.IsCancellationRequested)
                return;

            bool canPay = options.Options.Count > 0;

            if (!canPay)
            {
                // Nothing payable and no credits balance means the balance read failed, not that the buyer is short.
                if (options.CreditsBalanceCents is not { } balanceCents)
                {
                    ShowResult(FAILED_TITLE, "Could not load your balance.", allowRetry: true);
                    return;
                }

                if (viewInstance != null)
                    viewInstance.CannotAffordText.text = InsufficientCopy(options, balanceCents);
            }

            ShowBalances(options);
            viewInstance?.PaymentSelector.Show(options, isManaEnabled, MANA_ALONE_REFUSED_NOTE);
            SetUiState(canPay ? ModalState.ReadyToConfirm : ModalState.InsufficientFunds);
        }

        private string InsufficientCopy(PaymentOptions options, long balanceCents)
        {
            if (options.IsManaAloneRefused)
                return MANA_ALONE_REFUSED_TEXT;

            if (options.ManaShortfall != null)
                return INSUFFICIENT_FUNDS_TEXT;

            return string.Format(MISSING_CREDITS_TEXT, PaymentAmountFormat.Credits(quote.Credits - (balanceCents / PaymentOptionsCalculator.CENTS_PER_CREDIT)));
        }

        private void ShowBalances(PaymentOptions options)
        {
            if (viewInstance == null)
                return;

            viewInstance.BalanceCreditsText.text = PaymentAmountFormat.CreditsBalance(options.CreditsBalanceCents);
            viewInstance.BalanceManaText.text = PaymentAmountFormat.ManaBalance(options.ManaBalanceWei);
        }

        private void OnConfirmClicked()
        {
            if (currentState != ModalState.ReadyToConfirm || viewInstance == null || lifeCts == null)
                return;

            if (!viewInstance.PaymentSelector.Selection.TryGetSelected(out PaymentOption option))
                return;

            if (claimService.InFlightName != null)
            {
                ShowResult(FAILED_TITLE, CLAIM_IN_FLIGHT_TEXT, allowRetry: true);
                return;
            }

            ClaimAsync(option, lifeCts.Token).Forget();
        }

        private void OnRetryClicked()
        {
            if (currentState != ModalState.Result || lifeCts == null)
                return;

            LoadQuoteAndOptionsAsync(lifeCts.Token).Forget();
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

        private async UniTaskVoid ClaimAsync(PaymentOption option, CancellationToken ct)
        {
            SetUiState(ModalState.Claiming);
            NativeWindowManager.RequestTemporaryWindowMode();
            var windowModeHeld = true;

            // The window mode is only needed for the wallet signatures, not for the settlement and bridge waits after the payment's one.
            void ReleaseWindowModeOncePaymentSigned(NameClaimStage stage)
            {
                if (!windowModeHeld || stage is NameClaimStage.Preparing or NameClaimStage.ApprovingMana or NameClaimStage.Signing)
                    return;

                windowModeHeld = false;
                NativeWindowManager.ReleaseTemporaryWindowMode();
            }

            claimService.StageChanged += ReleaseWindowModeOncePaymentSigned;

            try { await claimService.ClaimAsync(inputData.Name, quote, option, ct); }
            finally
            {
                claimService.StageChanged -= ReleaseWindowModeOncePaymentSigned;

                if (windowModeHeld)
                    NativeWindowManager.ReleaseTemporaryWindowMode();
            }
        }

        private void OnClaimCompleted(string name, NameClaimResult result)
        {
            if (currentState != ModalState.Claiming || name != inputData.Name)
                return;

            claimService.AcknowledgeResult();
            ShowClaimResult(result);
        }

        private void ShowClaimResult(NameClaimResult result)
        {
            switch (result.Status)
            {
                case NameClaimStatus.Registered:
                    SetUiState(ModalState.Success);

                    if (viewInstance != null)
                        viewInstance.SuccessText.text = string.Format(SUCCESS_TEXT, inputData.Name);

                    if (lifeCts != null)
                        RefreshBalancesAsync(lifeCts.Token).Forget();

                    break;
                case NameClaimStatus.Pending:
                    ShowResult(PENDING_TITLE, string.Format(PENDING_TEXT, inputData.Name), allowRetry: false);
                    break;
                case NameClaimStatus.Failed when result.Error == NameClaimError.Cancelled:
                    // The quote and balances on screen may predate the cancelled claim, or be missing after a reopen.
                    if (lifeCts != null)
                        LoadQuoteAndOptionsAsync(lifeCts.Token).Forget();

                    break;
                default:
                    ShowError(result.Error);
                    break;
            }
        }

        private async UniTaskVoid RefreshBalancesAsync(CancellationToken ct)
        {
            PaymentOptions options = await claimService.GetPaymentOptionsAsync(quote, ct);

            // A failed read keeps the balances the popup already has.
            if (!ct.IsCancellationRequested && options.CreditsBalanceCents != null)
                ShowBalances(options);
        }

        // Retry is offered only for failures that provably never spent anything.
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
                case NameClaimError.InsufficientMana:
                    ShowResult(FAILED_TITLE, "You do not have enough MANA to claim this NAME.", allowRetry: true);
                    break;
                case NameClaimError.ManaApprovalFailed:
                    ShowResult(FAILED_TITLE, "MANA could not be authorized for this claim. Nothing was spent.", allowRetry: true);
                    break;
                case NameClaimError.PriceChanged:
                    ShowResult(FAILED_TITLE, "The price changed. Review it and try again.", allowRetry: true);
                    break;
                case NameClaimError.SignatureRejected:
                    ShowResult(FAILED_TITLE, "The signature was rejected. Nothing was spent.", allowRetry: true);
                    break;
                case NameClaimError.SignatureTimedOut:
                    ShowResult(FAILED_TITLE, "The confirmation timed out. Nothing was spent.", allowRetry: true);
                    break;
                case NameClaimError.TransactionReverted:
                    ShowResult(FAILED_TITLE, "The payment failed on-chain. Nothing was spent.", allowRetry: true);
                    break;
                case NameClaimError.PurchaseInProgress:
                    ShowResult(FAILED_TITLE, "Another purchase is still processing. Wait for it to finish and try again.", allowRetry: true);
                    break;
                case NameClaimError.NotRegistered:
                    ShowResult(FAILED_TITLE, "The payment went through but the NAME could not be registered. Please contact support.", allowRetry: false);
                    break;
                case NameClaimError.Cancelled:
                    SetUiState(ModalState.ReadyToConfirm);
                    break;
                default:
                    ShowResult(FAILED_TITLE, "The claim failed. Nothing was spent.", allowRetry: true);
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
                                                       NameClaimStage.ApprovingMana => "Waiting for you to authorize MANA...",
                                                       NameClaimStage.Signing => "Waiting for your signature in the browser",
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

            bool insufficient = newState == ModalState.InsufficientFunds;
            bool confirming = insufficient || newState is ModalState.Loading or ModalState.ReadyToConfirm;

            viewInstance.ConfirmStateContainer.SetActive(confirming);
            viewInstance.ProgressStateContainer.SetActive(newState == ModalState.Claiming);
            viewInstance.SuccessStateContainer.SetActive(newState == ModalState.Success);
            viewInstance.ResultStateContainer.SetActive(newState == ModalState.Result);

            viewInstance.TitleText.text = insufficient ? INSUFFICIENT_FUNDS_TITLE : BUY_TITLE;
            viewInstance.InsufficientIcon.SetActive(insufficient);
            viewInstance.CannotAffordText.gameObject.SetActive(insufficient);
            viewInstance.ConfirmButton.gameObject.SetActive(!insufficient);
            viewInstance.ConfirmButton.interactable = newState == ModalState.ReadyToConfirm;
            viewInstance.GetCreditsButton.gameObject.SetActive(insufficient);

            // The confirmation has its own Cancel button.
            viewInstance.CloseButton.gameObject.SetActive(!confirming);
        }
    }
}
