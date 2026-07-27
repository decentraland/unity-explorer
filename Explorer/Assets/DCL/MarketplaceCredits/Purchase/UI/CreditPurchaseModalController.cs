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
            LOADING_BALANCE,
            READY_TO_CONFIRM,
            INSUFFICIENT_CREDITS,
            PURCHASING,
            SUCCESS,
            FAILED,
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

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.POPUP;

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

        private static bool CanAfford(ShopListingDto listing, in UserCreditsResponse credits) =>
            credits.usd.credits >= listing.priceCredits;

        protected override void OnViewShow()
        {
            lifeCts = new CancellationTokenSource();
            settlementPending = false;

            if (viewInstance != null)
            {
                viewInstance.ItemName.text = inputData.ItemName;
                viewInstance.RarityLabel.text = inputData.RarityName;
                viewInstance.RarityLabel.color = inputData.RarityColor;
                viewInstance.PriceCreditsText.text = inputData.Listing.priceCredits.ToString();

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

            LoadBalanceAndArmAsync(lifeCts.Token).Forget();
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
                viewInstance.DoneButton.OnClickAsync(ct),
                viewInstance.CloseBackground.OnClickAsync(ct));
        }

        private async UniTask LoadBalanceAndArmAsync(CancellationToken ct)
        {
            SetUiState(ModalState.LOADING_BALANCE);

            IWeb3Identity? identity = identityCache.Identity;

            if (identity == null)
            {
                ShowFailure("You need to be signed in to buy items.", allowRetry: false);
                return;
            }

            try
            {
                UserCreditsResponse credits = await creditsAPIClient.GetUserCreditsAsync(identity.Address, ct);

                if (ct.IsCancellationRequested)
                    return;

                if (viewInstance != null)
                    viewInstance.BalanceCreditsText.text = credits.usd.credits.ToString();

                SetUiState(CanAfford(inputData.Listing, credits) ? ModalState.READY_TO_CONFIRM : ModalState.INSUFFICIENT_CREDITS);
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
            if (currentState != ModalState.READY_TO_CONFIRM || lifeCts == null || lifeCts.IsCancellationRequested)
                return;

            PurchaseAsync(lifeCts.Token).Forget();
        }

        private void OnRetryClicked()
        {
            if (currentState != ModalState.FAILED || settlementPending || lifeCts == null || lifeCts.IsCancellationRequested)
                return;

            LoadBalanceAndArmAsync(lifeCts.Token).Forget();
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

        private async UniTask PurchaseAsync(CancellationToken ct)
        {
            SetUiState(ModalState.PURCHASING);
            NativeWindowManager.RequestTemporaryWindowMode();

            try
            {
                CreditsPurchaseResult result = await purchaseService.PurchaseAsync(inputData.Listing.tradeId, inputData.Listing.priceCredits, ct);

                if (result.Success)
                {
                    SetUiState(ModalState.SUCCESS);
                    RefreshBalanceAsync(lifeCts?.Token ?? CancellationToken.None).Forget();
                }
                else
                    ShowPurchaseError(result);
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

        private void ShowPurchaseError(in CreditsPurchaseResult result)
        {
            switch (result.Error)
            {
                case CreditsPurchaseError.CANCELLED:
                    SetUiState(ModalState.READY_TO_CONFIRM);
                    break;
                case CreditsPurchaseError.INSUFFICIENT_CREDITS:
                    SetUiState(ModalState.INSUFFICIENT_CREDITS);
                    break;
                case CreditsPurchaseError.SETTLEMENT_PENDING:
                    settlementPending = true;
                    ShowFailure("Your purchase is still processing. Your credits are reserved and the purchase will complete automatically — check back soon.", allowRetry: false);
                    break;
                case CreditsPurchaseError.SIGNATURE_REJECTED:
                    ShowFailure("The signature request was rejected.", allowRetry: true);
                    break;
                case CreditsPurchaseError.PRICE_CHANGED:
                    ShowFailure("The price of this item changed. Please reopen the item to see the new price.", allowRetry: false);
                    break;
                case CreditsPurchaseError.LISTING_NOT_AVAILABLE:
                    ShowFailure("This item is no longer available for purchase with credits.", allowRetry: false);
                    break;
                case CreditsPurchaseError.OWN_LISTING:
                    ShowFailure("You cannot buy your own listing.", allowRetry: false);
                    break;
                case CreditsPurchaseError.TRANSACTION_REVERTED:
                    ShowFailure("The purchase failed on-chain. Your credits were not spent.", allowRetry: true);
                    break;
                case CreditsPurchaseError.RELAYER_UNAVAILABLE:
                    ShowFailure("The purchase service is temporarily unavailable. Please try again later.", allowRetry: true);
                    break;
                default:
                    ShowFailure("The purchase failed. Your credits were not spent.", allowRetry: true);
                    break;
            }
        }

        private void OnPurchaseStateChanged(CreditsPurchaseState state)
        {
            if (viewInstance == null || currentState != ModalState.PURCHASING)
                return;

            viewInstance.ProgressStatusText.text = state switch
            {
                CreditsPurchaseState.RESOLVING_LISTING => "Checking availability...",
                CreditsPurchaseState.AUTHORIZING => "Reserving your credits...",
                CreditsPurchaseState.SIGNING => "Waiting for your signature...",
                CreditsPurchaseState.SUBMITTING => "Submitting the purchase...",
                CreditsPurchaseState.WAITING_SETTLEMENT => "Completing the purchase...",
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
            SetUiState(ModalState.FAILED);

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

            bool purchasing = newState == ModalState.PURCHASING;

            switch (newState)
            {
                case ModalState.SUCCESS:
                    viewInstance.ContainerTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, COMPLETED_HEIGHT);
                    break;
                case ModalState.PURCHASING:
                    viewInstance.ContainerTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, PURCHASING_HEIGHT);
                    break;
                case ModalState.INSUFFICIENT_CREDITS:
                    viewInstance.ContainerTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, INSUFFICIENT_CREDITS_HEIGHT);
                    break;
                default:
                    viewInstance.ContainerTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, NORMAL_HEIGHT);
                    break;
            }
            viewInstance.ConfirmStateContainer.SetActive(newState is ModalState.LOADING_BALANCE or ModalState.READY_TO_CONFIRM or ModalState.INSUFFICIENT_CREDITS);
            viewInstance.ProgressStateContainer.SetActive(purchasing);
            viewInstance.SuccessStateContainer.SetActive(newState == ModalState.SUCCESS);
            viewInstance.FailedStateContainer.SetActive(newState == ModalState.FAILED);
            viewInstance.InsufficientCreditsContainer.SetActive(newState == ModalState.INSUFFICIENT_CREDITS);
            viewInstance.BalanceLoadingSpinner.SetActive(newState == ModalState.LOADING_BALANCE);
            viewInstance.Item.SetActive(newState is ModalState.LOADING_BALANCE or ModalState.READY_TO_CONFIRM or ModalState.INSUFFICIENT_CREDITS);

            viewInstance.ConfirmButton.interactable = newState == ModalState.READY_TO_CONFIRM;
            viewInstance.CloseButton.interactable = !purchasing;
            viewInstance.CancelButton.interactable = !purchasing;
        }

        private void RequestClose() =>
            viewInstance?.CloseButton.onClick.Invoke();
    }
}
