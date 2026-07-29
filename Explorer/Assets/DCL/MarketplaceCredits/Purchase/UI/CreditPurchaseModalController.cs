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

        private const float NORMAL_HEIGHT = 491;
        private const float PURCHASING_HEIGHT = 371;
        private const float INSUFFICIENT_CREDITS_HEIGHT = 622;
        private const float COMPLETED_HEIGHT = 571;

        private readonly ICreditsPurchaseService purchaseService;
        private readonly MarketplaceCreditsAPIClient creditsAPIClient;
        private readonly IWeb3IdentityCache identityCache;
        private readonly UnityAppWebBrowser webBrowser;
        private readonly Func<CancellationToken, UniTask> openGetCreditsPanelAsync;
        private readonly CancellationTokenSource disposalCts = new ();

        private ModalState currentState;
        private bool settlementPending;
        private CancellationTokenSource? lifeCts;
        private CreditsPurchaseQuote? quote;

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Popup;

        public CreditPurchaseModalController(
            ViewFactoryMethod viewFactory,
            ICreditsPurchaseService purchaseService,
            MarketplaceCreditsAPIClient creditsAPIClient,
            IWeb3IdentityCache identityCache,
            UnityAppWebBrowser webBrowser,
            Func<CancellationToken, UniTask> openGetCreditsPanelAsync)
            : base(viewFactory)
        {
            this.purchaseService = purchaseService;
            this.creditsAPIClient = creditsAPIClient;
            this.identityCache = identityCache;
            this.webBrowser = webBrowser;
            this.openGetCreditsPanelAsync = openGetCreditsPanelAsync;
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

            if (viewInstance != null)
            {
                viewInstance.ItemName.text = inputData.ItemName;
                viewInstance.RarityLabel.text = inputData.RarityName;
                viewInstance.RarityLabel.color = inputData.RarityColor;
                viewInstance.RarityBackground.color = new Color(inputData.RarityColor.r, inputData.RarityColor.g, inputData.RarityColor.b, viewInstance.RarityBackground.color.a);
                viewInstance.PriceCreditsText.text = string.Empty;

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
                viewInstance.OpenMarketplaceButton.onClick.RemoveListener(OnOpenMarketplaceClicked);
            }

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
                viewInstance.DoneButton.OnClickAsync(ct),
                viewInstance.CloseBackground.OnClickAsync(ct));
        }

        private async UniTask LoadQuoteAndBalanceAsync(CancellationToken ct)
        {
            SetUiState(ModalState.LoadingBalance);

            IWeb3Identity? identity = identityCache.Identity;

            if (identity == null)
            {
                ShowFailure("You need to be signed in to buy items.", allowRetry: false);
                return;
            }

            CreditsQuoteResult quoteResult = await purchaseService.QuoteAsync(inputData.Listing.tradeId, ct);

            if (ct.IsCancellationRequested)
                return;

            if (!quoteResult.Success)
            {
                ReportHub.LogWarning(ReportCategory.CREDITS_PURCHASE, $"Quote failed for trade {inputData.Listing.tradeId}: {quoteResult.Error} {quoteResult.Message}");
                ShowError(quoteResult.Error);
                return;
            }

            CreditsPurchaseQuote resolved = quoteResult.Quote;
            quote = resolved;

            if (viewInstance != null)
                viewInstance.PriceCreditsText.text = resolved.IsLiveRatePrice ? $"≈{resolved.Credits}" : resolved.Credits.ToString();

            try
            {
                UserCreditsResponse credits = await creditsAPIClient.GetUserCreditsAsync(identity.Address, ct);

                if (ct.IsCancellationRequested)
                    return;

                if (viewInstance != null)
                    viewInstance.BalanceCreditsText.text = credits.usd.credits.ToString();

                SetUiState(CanAfford(resolved, credits) ? ModalState.ReadyToConfirm : ModalState.InsufficientCredits);
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                ReportHub.LogException(e, new ReportData(ReportCategory.CREDITS_PURCHASE));
                ShowFailure("Could not load your credits balance.", allowRetry: true);
            }
        }

        private void OnConfirmClicked()
        {
            if (currentState != ModalState.ReadyToConfirm || quote == null || lifeCts == null || lifeCts.IsCancellationRequested)
                return;

            PurchaseAsync(quote.Value, lifeCts.Token).Forget();
        }

        private void OnRetryClicked()
        {
            if (currentState != ModalState.Failed || settlementPending || lifeCts == null || lifeCts.IsCancellationRequested)
                return;

            LoadQuoteAndBalanceAsync(lifeCts.Token).Forget();
        }

        private void OnGetCreditsClicked()
        {
            RequestClose();
            OpenGetCreditsAfterCloseAsync(disposalCts.Token).Forget();
        }

        private void OnOpenMarketplaceClicked()
        {
            if (!string.IsNullOrEmpty(inputData.FallbackMarketplaceUrl))
                webBrowser.OpenUrlMainThreadOnly(inputData.FallbackMarketplaceUrl);
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
                    SetUiState(ModalState.Success);
                    RefreshBalanceAsync(lifeCts?.Token ?? CancellationToken.None).Forget();
                }
                else
                    ShowError(result.Error);
            }
            catch (Exception e)
            {
                ReportHub.LogException(e, new ReportData(ReportCategory.CREDITS_PURCHASE));
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
                UserCreditsResponse credits = await creditsAPIClient.GetUserCreditsAsync(identity.Address, ct);

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

        private void ShowFailure(string reason, bool allowRetry)
        {
            SetUiState(ModalState.Failed);

            if (viewInstance == null)
                return;

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
            viewInstance.Item.SetActive(newState is ModalState.LoadingBalance or ModalState.ReadyToConfirm or ModalState.InsufficientCredits);

            viewInstance.ConfirmButton.interactable = newState == ModalState.ReadyToConfirm;
        }

        private void RequestClose() =>
            viewInstance?.CloseButton.onClick.Invoke();
    }
}
