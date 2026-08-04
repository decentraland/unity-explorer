using Cysharp.Threading.Tasks;
using DCL.Browser;
using DCL.Diagnostics;
using DCL.Web3.Identities;
using MVC;
using Plugins.NativeWindowManager;
using System;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.MarketplaceCredits.Purchase.UI
{
    public class CreditPurchaseModalController : ControllerBase<CreditPurchaseModalView, CreditPurchaseModalControllerParams>
    {
        private enum ModalState
        {
            LoadingBalance,
            ReadyToConfirm,
            InsufficientCredits,
            Purchasing,
            Success,
            Failed,
        }

        public const string NAV_DESTINATION_GET_CREDITS = "get_credits";
        public const string NAV_DESTINATION_BACKPACK = "backpack";
        public const string NAV_DESTINATION_MARKETPLACE = "marketplace";

        private const string CANNOT_AFFORD_TEXT = "Add <b>{0} Credits</b> to complete your purchase.";
        private const float NORMAL_HEIGHT = 491;
        private const float PURCHASING_HEIGHT = 371;
        private const float INSUFFICIENT_CREDITS_HEIGHT = 622;
        private const float COMPLETED_HEIGHT = 571;

        private const string ANALYTICS_STEP_QUOTE = "quote";
        private const string ANALYTICS_STEP_BALANCE = "balance";
        private const string ANALYTICS_ERROR_UNKNOWN = "unknown";
        private const string ANALYTICS_DETAIL_NO_IDENTITY = "no_identity";
        private const string ANALYTICS_DETAIL_BALANCE_LOAD_FAILED = "balance_load_failed";
        private const string ANALYTICS_DETAIL_UNHANDLED_EXCEPTION = "unhandled_exception";
        private const int UNKNOWN_MISSING_CREDITS = -1;

        private readonly ICreditsPurchaseService purchaseService;
        private readonly MarketplaceCreditsAPIClient creditsApiClient;
        private readonly IWeb3IdentityCache identityCache;
        private readonly UnityAppWebBrowser webBrowser;
        private readonly Func<CancellationToken, UniTask> openGetCreditsPanelAsync;
        private readonly Func<CancellationToken, UniTask> openBackpackAsync;
        private readonly CancellationTokenSource disposalCts = new ();

        private ModalState currentState;
        private bool settlementPending;
        private CancellationTokenSource? lifeCts;
        private CreditsPurchaseQuote? quote;
        private bool purchaseSucceeded;
        private bool navigatedAway;
        private float purchaseStartedAt;
        private CreditsPurchaseState lastPurchaseState;

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Popup;

        public event Action<ShopListingDto, string>? ModalOpened;
        public event Action<ShopListingDto, int>? BuyCreditsPrompted;
        public event Action<ShopListingDto, CreditsPurchaseQuote>? PurchaseStarted;
        public event Action<ShopListingDto, CreditsPurchaseQuote, string, float>? PurchaseCompleted;
        public event Action<ShopListingDto, string, string, string>? PurchaseFailed;
        public event Action<ShopListingDto, string>? PurchaseCancelled;
        public event Action<ShopListingDto, string, string>? NavigationClicked;
        public event Action<ShopListingDto>? RetryClicked;

        public CreditPurchaseModalController(
            ViewFactoryMethod viewFactory,
            ICreditsPurchaseService purchaseService,
            MarketplaceCreditsAPIClient creditsApiClient,
            IWeb3IdentityCache identityCache,
            UnityAppWebBrowser webBrowser,
            Func<CancellationToken, UniTask> openGetCreditsPanelAsync,
            Func<CancellationToken, UniTask> openBackpackAsync)
            : base(viewFactory)
        {
            this.purchaseService = purchaseService;
            this.creditsApiClient = creditsApiClient;
            this.identityCache = identityCache;
            this.webBrowser = webBrowser;
            this.openGetCreditsPanelAsync = openGetCreditsPanelAsync;
            this.openBackpackAsync = openBackpackAsync;
            purchaseService.StateChanged += OnPurchaseStateChanged;
        }

        public override void Dispose()
        {
            purchaseService.StateChanged -= OnPurchaseStateChanged;
            lifeCts?.SafeCancelAndDispose();
            disposalCts.SafeCancelAndDispose();
        }

        private static bool CanAfford(in CreditsPurchaseQuote quote, in UserCreditsResponse credits) =>
            credits.usd.credits >= quote.Credits;

        protected override void OnViewShow()
        {
            lifeCts = new CancellationTokenSource();
            settlementPending = false;
            quote = null;
            purchaseSucceeded = false;
            navigatedAway = false;
            lastPurchaseState = CreditsPurchaseState.ResolvingListing;

            if (viewInstance != null)
            {
                viewInstance.ItemName.text = inputData.ItemName;
                viewInstance.RarityLabel.text = inputData.RarityName;
                viewInstance.RarityLabel.color = inputData.RarityColor;
                viewInstance.RarityBackground.color = new Color(inputData.RarityColor.r, inputData.RarityColor.g, inputData.RarityColor.b, viewInstance.RarityBackground.color.a);

                if (inputData.ItemThumbnail != null)
                {
                    viewInstance.ItemThumbnail.sprite = inputData.ItemThumbnail;
                    viewInstance.ItemThumbnailCompleted.sprite = inputData.ItemThumbnail;
                }

                if (inputData.RarityBackground != null)
                {
                    viewInstance.ItemBackground.sprite = inputData.RarityBackground;
                    viewInstance.ItemBackgroundCompleted.sprite = inputData.RarityBackground;
                }

                if (inputData.CategoryIcon != null)
                {
                    viewInstance.ItemCategory.sprite = inputData.CategoryIcon;
                    viewInstance.ItemCategoryCompleted.sprite = inputData.CategoryIcon;
                }

                viewInstance.ItemCategoryBackground.color = inputData.RarityColor;
                viewInstance.ItemCategoryBackgroundCompleted.color = inputData.RarityColor;

                viewInstance.ConfirmButton.onClick.AddListener(OnConfirmClicked);
                viewInstance.RetryButton.onClick.AddListener(OnRetryClicked);
                viewInstance.GetCreditsButton.onClick.AddListener(OnGetCreditsClicked);
                viewInstance.OpenMarketplaceButton.onClick.AddListener(OnOpenMarketplaceClicked);
                viewInstance.ToBackpackButton.onClick.AddListener(OnToBackpackClicked);
            }

            LoadQuoteAndBalanceAsync(lifeCts.Token).Forget();

            ModalOpened?.Invoke(inputData.Listing, inputData.Source);
        }

        protected override void OnViewClose()
        {
            if (viewInstance != null)
            {
                viewInstance.ConfirmButton.onClick.RemoveListener(OnConfirmClicked);
                viewInstance.RetryButton.onClick.RemoveListener(OnRetryClicked);
                viewInstance.GetCreditsButton.onClick.RemoveListener(OnGetCreditsClicked);
                viewInstance.OpenMarketplaceButton.onClick.RemoveListener(OnOpenMarketplaceClicked);
                viewInstance.ToBackpackButton.onClick.RemoveListener(OnToBackpackClicked);
            }

            if (!purchaseSucceeded && !navigatedAway)
                PurchaseCancelled?.Invoke(inputData.Listing, MapAnalyticsStageName(currentState));

            lifeCts.SafeCancelAndDispose();
        }

        protected override async UniTask WaitForCloseIntentAsync(CancellationToken ct)
        {
            if (viewInstance == null)
                return;

            await UniTask.WhenAny(
                viewInstance.CloseButton.OnClickAsync(ct),
                viewInstance.CancelButton.OnClickAsync(ct),
                viewInstance.InsufficientCancelButton.OnClickAsync(ct),
                viewInstance.CloseBackground.OnClickAsync(ct));
        }

        private async UniTask LoadQuoteAndBalanceAsync(CancellationToken ct)
        {
            SetUiState(ModalState.LoadingBalance);

            if (viewInstance != null)
            {
                viewInstance.PriceCreditsText.text = string.Empty;
                viewInstance.PriceLoadingSpinner.SetActive(true);
            }

            IWeb3Identity? identity = identityCache.Identity;

            if (identity == null)
            {
                PurchaseFailed?.Invoke(inputData.Listing, ANALYTICS_STEP_QUOTE, ANALYTICS_ERROR_UNKNOWN, ANALYTICS_DETAIL_NO_IDENTITY);
                ShowFailure("You need to be signed in to buy items.", allowRetry: false);
                return;
            }

            CreditsQuoteResult quoteResult = await purchaseService.QuoteAsync(inputData.Listing.tradeId, ct);

            if (ct.IsCancellationRequested)
                return;

            if (!quoteResult.Success)
            {
                ReportHub.LogWarning(ReportCategory.CREDITS_PURCHASE, $"Quote failed for trade {inputData.Listing.tradeId}: {quoteResult.Error} {quoteResult.Message}");
                PurchaseFailed?.Invoke(inputData.Listing, ANALYTICS_STEP_QUOTE, MapAnalyticsErrorCode(quoteResult.Error), MapAnalyticsErrorDetail(quoteResult.Error));
                ShowError(quoteResult.Error);
                return;
            }

            CreditsPurchaseQuote resolved = quoteResult.Quote;
            quote = resolved;

            if (viewInstance != null)
            {
                viewInstance.PriceLoadingSpinner.SetActive(false);
                viewInstance.PriceCreditsText.text = resolved.IsLiveRatePrice ? $"≈{resolved.Credits}" : resolved.Credits.ToString();
            }

            try
            {
                UserCreditsResponse credits = await creditsApiClient.GetUserCreditsAsync(identity.Address, ct);

                if (ct.IsCancellationRequested)
                    return;

                bool canAfford = CanAfford(resolved, credits);

                if (viewInstance != null) {
                    viewInstance.CannotAffortText.text = string.Format(CANNOT_AFFORD_TEXT, resolved.Credits - credits.usd.credits);
                    viewInstance.BalanceCreditsText.text = credits.usd.credits.ToString();
                }

                SetUiState(canAfford ? ModalState.ReadyToConfirm : ModalState.InsufficientCredits);

                if (!canAfford)
                    BuyCreditsPrompted?.Invoke(inputData.Listing, resolved.Credits - credits.usd.credits);
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                ReportHub.LogException(e, new ReportData(ReportCategory.CREDITS_PURCHASE));
                PurchaseFailed?.Invoke(inputData.Listing, ANALYTICS_STEP_BALANCE, ANALYTICS_ERROR_UNKNOWN, ANALYTICS_DETAIL_BALANCE_LOAD_FAILED);
                ShowFailure("Could not load your credits balance.", allowRetry: true);
            }
        }

        private void OnConfirmClicked()
        {
            if (currentState != ModalState.ReadyToConfirm || quote == null || lifeCts == null || lifeCts.IsCancellationRequested)
                return;

            purchaseStartedAt = UnityEngine.Time.realtimeSinceStartup;
            lastPurchaseState = CreditsPurchaseState.ResolvingListing;
            PurchaseStarted?.Invoke(inputData.Listing, quote.Value);
            PurchaseAsync(quote.Value, lifeCts.Token).Forget();
        }

        private void OnRetryClicked()
        {
            if (currentState != ModalState.Failed || settlementPending || lifeCts == null || lifeCts.IsCancellationRequested)
                return;

            RetryClicked?.Invoke(inputData.Listing);
            LoadQuoteAndBalanceAsync(lifeCts.Token).Forget();
        }

        private void OnGetCreditsClicked()
        {
            NavigationClicked?.Invoke(inputData.Listing, NAV_DESTINATION_GET_CREDITS, MapAnalyticsStageName(currentState));
            navigatedAway = true;
            RequestClose();
            OpenGetCreditsAfterCloseAsync(disposalCts.Token).Forget();
        }

        private void OnToBackpackClicked()
        {
            NavigationClicked?.Invoke(inputData.Listing, NAV_DESTINATION_BACKPACK, MapAnalyticsStageName(currentState));
            navigatedAway = true;
            RequestClose();
            OpenBackpackAfterCloseAsync(disposalCts.Token).Forget();
        }

        private void OnOpenMarketplaceClicked()
        {
            if (!string.IsNullOrEmpty(inputData.FallbackMarketplaceUrl))
            {
                NavigationClicked?.Invoke(inputData.Listing, NAV_DESTINATION_MARKETPLACE, MapAnalyticsStageName(currentState));
                webBrowser.OpenUrlMainThreadOnly(inputData.FallbackMarketplaceUrl);
            }
        }

        private async UniTask PurchaseAsync(CreditsPurchaseQuote confirmedQuote, CancellationToken ct)
        {
            SetUiState(ModalState.Purchasing);
            NativeWindowManager.RequestTemporaryWindowMode();

            try
            {
                CreditsPurchaseResult result = await purchaseService.PurchaseAsync(confirmedQuote, ct);

                if (result.Success)
                {
                    purchaseSucceeded = true;
                    PurchaseCompleted?.Invoke(inputData.Listing, confirmedQuote, result.TxHash!, UnityEngine.Time.realtimeSinceStartup - purchaseStartedAt);
                    SetUiState(ModalState.Success);
                    RefreshBalanceAsync(lifeCts?.Token ?? CancellationToken.None).Forget();
                }
                else
                {
                    // Cancelled is the modal-close path, reported once as PurchaseCancelled, not as a failure.
                    if (result.Error != CreditsPurchaseError.Cancelled)
                        PurchaseFailed?.Invoke(inputData.Listing, MapAnalyticsStepName(lastPurchaseState), MapAnalyticsErrorCode(result.Error), MapAnalyticsErrorDetail(result.Error));

                    ShowError(result.Error);
                }
            }
            catch (Exception e)
            {
                ReportHub.LogException(e, new ReportData(ReportCategory.CREDITS_PURCHASE));
                PurchaseFailed?.Invoke(inputData.Listing, MapAnalyticsStepName(lastPurchaseState), ANALYTICS_ERROR_UNKNOWN, ANALYTICS_DETAIL_UNHANDLED_EXCEPTION);
                ShowFailure("Something went wrong. Please try again.", allowRetry: true);
            }
            finally
            {
                NativeWindowManager.ReleaseTemporaryWindowMode();
            }
        }

        private void ShowError(CreditsPurchaseError error)
        {
            switch (error)
            {
                case CreditsPurchaseError.Cancelled:
                    SetUiState(ModalState.ReadyToConfirm);
                    break;
                case CreditsPurchaseError.InsufficientCredits:
                    SetUiState(ModalState.InsufficientCredits);
                    BuyCreditsPrompted?.Invoke(inputData.Listing, UNKNOWN_MISSING_CREDITS);
                    break;
                case CreditsPurchaseError.SettlementPending:
                    settlementPending = true;
                    ShowFailure("Your purchase is still processing. Your credits are reserved and the purchase will complete automatically — check back soon.", allowRetry: false);
                    break;
                case CreditsPurchaseError.SignatureRejected:
                    ShowFailure("The signature request was rejected.", allowRetry: true);
                    break;
                case CreditsPurchaseError.PriceChanged:
                    ShowFailure("The price of this item changed. Please reopen the item to see the new price.", allowRetry: false);
                    break;
                case CreditsPurchaseError.PriceUnavailable:
                    ShowFailure("The price of this item is unavailable right now. Please try again later or open it in the marketplace.", allowRetry: true);
                    break;
                case CreditsPurchaseError.ListingNotAvailable:
                    ShowFailure("This item is no longer available for purchase with credits.", allowRetry: false);
                    break;
                case CreditsPurchaseError.OwnListing:
                    ShowFailure("You cannot buy your own listing.", allowRetry: false);
                    break;
                case CreditsPurchaseError.TransactionReverted:
                    ShowFailure("The purchase failed on-chain. Your credits were not spent.", allowRetry: true);
                    break;
                case CreditsPurchaseError.RelayerUnavailable:
                    ShowFailure("The purchase service is temporarily unavailable. Please try again later.", allowRetry: true);
                    break;
                default:
                    ShowFailure("The purchase failed. Your credits were not spent.", allowRetry: true);
                    break;
            }
        }

        private void OnPurchaseStateChanged(CreditsPurchaseState state)
        {
            // Terminal states carry no step information; keep the last progress step so a failure
            // can be attributed to where it happened. Captured before any view guard so it also
            // works view-less.
            if (state is not (CreditsPurchaseState.Success or CreditsPurchaseState.Failed))
                lastPurchaseState = state;

            if (viewInstance == null || currentState != ModalState.Purchasing)
                return;

            viewInstance.ProgressStatusText.text = state switch
            {
                CreditsPurchaseState.ResolvingListing => "Checking availability...",
                CreditsPurchaseState.Authorizing => "Reserving your credits...",
                CreditsPurchaseState.Signing => "Waiting for your signature...",
                CreditsPurchaseState.WaitingSettlement => "Completing the purchase...",
                _ => viewInstance.ProgressStatusText.text,
            };
        }

        private async UniTask RefreshBalanceAsync(CancellationToken ct)
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
                ReportHub.LogWarning(ReportCategory.CREDITS_PURCHASE, $"Post-purchase balance refresh failed: {e.Message}");
            }
        }

        private async UniTask OpenGetCreditsAfterCloseAsync(CancellationToken ct)
        {
            try { await openGetCreditsPanelAsync(ct); }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                ReportHub.LogException(e, new ReportData(ReportCategory.CREDITS_PURCHASE));
            }
        }

        private async UniTask OpenBackpackAfterCloseAsync(CancellationToken ct)
        {
            try { await openBackpackAsync(ct); }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                ReportHub.LogException(e, new ReportData(ReportCategory.CREDITS_PURCHASE));
            }
        }

        private void ShowFailure(string reason, bool allowRetry)
        {
            SetUiState(ModalState.Failed);

            if (viewInstance == null)
                return;

            viewInstance.PriceLoadingSpinner.SetActive(false);
            viewInstance.FailedReasonText.text = reason;
            viewInstance.RetryButton.gameObject.SetActive(allowRetry);
        }

        private void SetUiState(ModalState newState)
        {
            currentState = newState;

            if (viewInstance == null)
                return;

            bool purchasing = newState == ModalState.Purchasing;

            switch (newState)
            {
                case ModalState.Success:
                    viewInstance.ContainerTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, COMPLETED_HEIGHT);
                    break;
                case ModalState.Purchasing:
                    viewInstance.ContainerTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, PURCHASING_HEIGHT);
                    break;
                case ModalState.InsufficientCredits:
                    viewInstance.ContainerTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, INSUFFICIENT_CREDITS_HEIGHT);
                    break;
                default:
                    viewInstance.ContainerTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, NORMAL_HEIGHT);
                    break;
            }
            viewInstance.ConfirmStateContainer.SetActive(newState is ModalState.LoadingBalance or ModalState.ReadyToConfirm);
            viewInstance.ProgressStateContainer.SetActive(purchasing);
            viewInstance.SuccessStateContainer.SetActive(newState == ModalState.Success);
            viewInstance.FailedStateContainer.SetActive(newState == ModalState.Failed);
            viewInstance.InsufficientCreditsContainer.SetActive(newState == ModalState.InsufficientCredits);
            viewInstance.BalanceLoadingSpinner.SetActive(newState == ModalState.LoadingBalance);
            viewInstance.BalanceCreditsText.gameObject.SetActive(newState != ModalState.LoadingBalance);
            viewInstance.Item.SetActive(newState is ModalState.LoadingBalance or ModalState.ReadyToConfirm or ModalState.InsufficientCredits);

            viewInstance.ConfirmButton.interactable = newState == ModalState.ReadyToConfirm;
        }

        private void RequestClose() =>
            viewInstance?.CloseButton.onClick.Invoke();

        /// <summary>
        ///     Coarse analytics buckets, a superset of the web shop's
        ///     (user_rejected | insufficient_credits | not_for_sale | unknown).
        /// </summary>
        public static string MapAnalyticsErrorCode(CreditsPurchaseError error) =>
            error switch
            {
                CreditsPurchaseError.SignatureRejected => "user_rejected",
                CreditsPurchaseError.Cancelled => "user_rejected",
                CreditsPurchaseError.InsufficientCredits => "insufficient_credits",
                CreditsPurchaseError.ListingNotAvailable => "not_for_sale",
                CreditsPurchaseError.OwnListing => "not_for_sale",
                CreditsPurchaseError.FeatureDisabled => "not_for_sale",
                CreditsPurchaseError.PriceChanged => "price_error",
                CreditsPurchaseError.PriceUnavailable => "price_error",
                CreditsPurchaseError.SettlementPending => "settlement_pending",
                CreditsPurchaseError.TransactionReverted => "transaction_failed",
                CreditsPurchaseError.RelayerUnavailable => "service_unavailable",
                CreditsPurchaseError.AuthorizationFailed => "service_unavailable",
                _ => ANALYTICS_ERROR_UNKNOWN,
            };

        /// <summary>Raw error identifier for drill-down next to the coarse error_code bucket.</summary>
        public static string MapAnalyticsErrorDetail(CreditsPurchaseError error) =>
            error switch
            {
                CreditsPurchaseError.None => "none",
                CreditsPurchaseError.FeatureDisabled => "feature_disabled",
                CreditsPurchaseError.ListingNotAvailable => "listing_not_available",
                CreditsPurchaseError.OwnListing => "own_listing",
                CreditsPurchaseError.PriceChanged => "price_changed",
                CreditsPurchaseError.PriceUnavailable => "price_unavailable",
                CreditsPurchaseError.InsufficientCredits => "insufficient_credits",
                CreditsPurchaseError.AuthorizationFailed => "authorization_failed",
                CreditsPurchaseError.SignatureRejected => "signature_rejected",
                CreditsPurchaseError.SigningFailed => "signing_failed",
                CreditsPurchaseError.RelayerUnavailable => "relayer_unavailable",
                CreditsPurchaseError.TransactionReverted => "transaction_reverted",
                CreditsPurchaseError.SettlementPending => "settlement_pending",
                CreditsPurchaseError.Cancelled => "cancelled",
                CreditsPurchaseError.EncodingFailed => "encoding_failed",
                _ => "unknown_error",
            };

        /// <summary>The purchase step a failure is attributed to, from the last progress state.</summary>
        public static string MapAnalyticsStepName(CreditsPurchaseState state) =>
            state switch
            {
                CreditsPurchaseState.ResolvingListing => "resolving_listing",
                CreditsPurchaseState.Authorizing => "authorizing",
                CreditsPurchaseState.Signing => "signing",
                CreditsPurchaseState.WaitingSettlement => "waiting_settlement",
                _ => "purchase",
            };

        private static string MapAnalyticsStageName(ModalState state) =>
            state switch
            {
                ModalState.LoadingBalance => "loading_balance",
                ModalState.ReadyToConfirm => "ready_to_confirm",
                ModalState.InsufficientCredits => "insufficient_credits",
                ModalState.Purchasing => "purchasing",
                ModalState.Success => "success",
                _ => "failed",
            };
    }
}
