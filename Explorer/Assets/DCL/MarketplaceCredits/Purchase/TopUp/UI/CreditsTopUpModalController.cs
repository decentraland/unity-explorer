using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Web3.Identities;
using MVC;
using System;
using System.Threading;
using Utility;

namespace DCL.MarketplaceCredits.Purchase.TopUp.UI
{
    public class CreditsTopUpModalController : ControllerBase<CreditsTopUpModalView, CreditsTopUpModalControllerParams>
    {
        private enum ModalState
        {
            PACK_SELECTION,
            CREATING_CHECKOUT,
            WAITING_FOR_BROWSER,
            PENDING,
            SUCCESS,
            FAILED,
        }

        private const string ANALYTICS_STEP_CHECKOUT = "checkout";
        private const string ANALYTICS_STEP_GRANT = "grant";
        private const string ANALYTICS_ERROR_GRANT_FAILED = "grant_failed";
        private const string SUCCESS_TEXT = "YOU SUCCESFULLY BOUGHT {0} CREDITS, CURRENT BALANCE {1} CREDITS";
        private const string AVAILABLE_CREDITS_TEXT = "You own {0} credits";

        private readonly ICreditsTopUpService topUpService;
        private readonly MarketplaceCreditsAPIClient creditsAPIClient;
        private readonly IWeb3IdentityCache identityCache;

        private ModalState currentState;
        private CreditsTopUpStage lastStage = CreditsTopUpStage.IDLE;
        private bool isViewShown;
        private CancellationTokenSource? lifeCts;

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.POPUP;

        public event Action<CreditPack, string>? BuyCreditsStarted;
        public event Action<string, CreditPack>? RedirectedToStripe;
        public event Action<string, CreditPack>? BuyCreditsCompleted;
        public event Action<CreditPack>? BuyCreditsPending;
        public event Action<string, string, CreditPack>? BuyCreditsFailed;

        public CreditsTopUpModalController(
            ViewFactoryMethod viewFactory,
            ICreditsTopUpService topUpService,
            MarketplaceCreditsAPIClient creditsAPIClient,
            IWeb3IdentityCache identityCache)
            : base(viewFactory)
        {
            this.topUpService = topUpService;
            this.creditsAPIClient = creditsAPIClient;
            this.identityCache = identityCache;
            topUpService.StatusChanged += OnServiceStatusChanged;
        }

        public override void Dispose()
        {
            topUpService.StatusChanged -= OnServiceStatusChanged;
            lifeCts?.SafeCancelAndDispose();
        }

        protected override void OnViewShow()
        {
            lifeCts = new CancellationTokenSource();
            isViewShown = true;

            if (viewInstance != null)
            {
                BindPackItems();
                viewInstance.RetryButton.onClick.AddListener(OnRetryClicked);
            }

            ApplyStatus(topUpService.CurrentStatus);
            LoadBalanceAsync(lifeCts.Token).Forget();
        }

        protected override void OnViewClose()
        {
            isViewShown = false;

            if (viewInstance != null)
            {
                foreach (CreditsTopUpPackItemView packItem in viewInstance.PackItems)
                    packItem.BuyButton.onClick.RemoveAllListeners();

                viewInstance.RetryButton.onClick.RemoveListener(OnRetryClicked);
            }

            if (currentState == ModalState.WAITING_FOR_BROWSER)
                topUpService.StopWaitingForBrowser();
            else if (currentState is ModalState.SUCCESS or ModalState.FAILED or ModalState.PENDING)
                topUpService.AcknowledgeTerminalState();

            lifeCts.SafeCancelAndDispose();
        }

        protected override async UniTask WaitForCloseIntentAsync(CancellationToken ct)
        {
            if (viewInstance == null)
                return;

            await UniTask.WhenAny(
                viewInstance.CloseButton.OnClickAsync(ct),
                viewInstance.DoneButton.OnClickAsync(ct));
        }

        private void BindPackItems()
        {
            if (viewInstance == null)
                return;

            int count = Math.Min(viewInstance.PackItems.Length, CreditPackCatalog.PACKS.Length);

            for (var i = 0; i < count; i++)
            {
                CreditPack pack = CreditPackCatalog.PACKS[i];
                CreditsTopUpPackItemView packItem = viewInstance.PackItems[i];

                packItem.PriceText.text = $"${pack.PriceUsd}";
                packItem.CreditsText.text = pack.Credits.ToString();
                packItem.BestValueBadge.SetActive(pack.BestValue);
                packItem.BuyButton.onClick.AddListener(() => OnPackClicked(pack));
            }
        }

        private void OnPackClicked(CreditPack pack)
        {
            if (currentState != ModalState.PACK_SELECTION || topUpService.IsOrderInFlight)
                return;

            BuyCreditsStarted?.Invoke(pack, inputData.Source);
            topUpService.StartTopUp(pack);
        }

        private void OnRetryClicked()
        {
            if (currentState != ModalState.FAILED)
                return;

            topUpService.AcknowledgeTerminalState();
        }

        private void OnServiceStatusChanged(CreditsTopUpStatus status)
        {
            if (status.Stage != lastStage)
            {
                switch (status.Stage)
                {
                    case CreditsTopUpStage.WAITING_FOR_PAYMENT:
                        RedirectedToStripe?.Invoke(status.OrderId!, status.Pack);
                        break;
                    case CreditsTopUpStage.PENDING_TIMEOUT:
                        BuyCreditsPending?.Invoke(status.Pack);
                        break;
                    case CreditsTopUpStage.CREDITED:
                        BuyCreditsCompleted?.Invoke(status.OrderId!, status.Pack);
                        break;
                    case CreditsTopUpStage.FAILED:
                        BuyCreditsFailed?.Invoke(
                            status.CheckoutError != null ? ANALYTICS_STEP_CHECKOUT : ANALYTICS_STEP_GRANT,
                            MapAnalyticsErrorCode(status),
                            status.Pack);

                        break;
                }

                lastStage = status.Stage;
            }

            if (isViewShown)
                ApplyStatus(status);
        }

        private void ApplyStatus(in CreditsTopUpStatus status)
        {
            SetUiState(MapStage(status.Stage));

            if (viewInstance == null)
                return;

            switch (status.Stage)
            {
                case CreditsTopUpStage.CREDITED:
                    viewInstance.ResultText.text = string.Format(SUCCESS_TEXT, status.CreditsGranted, status.NewBalance);
                    viewInstance.BalanceCreditsText.text = string.Format(AVAILABLE_CREDITS_TEXT, status.NewBalance);
                    break;
                case CreditsTopUpStage.FAILED:
                    (string reason, bool allowRetry) = MapFailureCopy(status);
                    viewInstance.FailedReasonText.text = reason;
                    viewInstance.RetryButton.gameObject.SetActive(allowRetry);
                    break;
            }
        }

        private void SetUiState(ModalState newState)
        {
            currentState = newState;

            if (viewInstance == null)
                return;

            bool creatingCheckout = newState == ModalState.CREATING_CHECKOUT;
            bool packsVisible = newState is ModalState.PACK_SELECTION or ModalState.CREATING_CHECKOUT;
            bool fullScreenState = newState is ModalState.WAITING_FOR_BROWSER or ModalState.SUCCESS;

            viewInstance.HeaderContainer.SetActive(!fullScreenState);
            viewInstance.BalanceContainer.SetActive(!fullScreenState);

            viewInstance.PackSelectionContainer.SetActive(packsVisible);
            viewInstance.WaitingForBrowserContainer.SetActive(newState == ModalState.WAITING_FOR_BROWSER);
            viewInstance.SuccessContainer.SetActive(newState == ModalState.SUCCESS);
            viewInstance.FailedContainer.SetActive(newState == ModalState.FAILED);
            viewInstance.DoneButton.gameObject.SetActive(newState is ModalState.SUCCESS or ModalState.PENDING);

            foreach (CreditsTopUpPackItemView packItem in viewInstance.PackItems)
                packItem.BuyButton.interactable = newState == ModalState.PACK_SELECTION;

            // Close is locked only while creating the checkout; during the browser wait the X acts as
            // "stop waiting" and hands the order off to the background poll.
            viewInstance.CloseButton.interactable = newState != ModalState.CREATING_CHECKOUT;
        }

        private async UniTask LoadBalanceAsync(CancellationToken ct)
        {
            IWeb3Identity? identity = identityCache.Identity;

            if (identity == null)
                return;

            if (viewInstance != null)
                viewInstance.BalanceLoadingSpinner.SetActive(true);

            try
            {
                UserCreditsResponse credits = await creditsAPIClient.GetUserCreditsAsync(identity.Address, ct);

                if (!ct.IsCancellationRequested && viewInstance != null)
                    viewInstance.BalanceCreditsText.text = viewInstance.BalanceCreditsText.text = string.Format(AVAILABLE_CREDITS_TEXT, credits.usd.credits.ToString());
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                ReportHub.LogWarning(ReportCategory.CREDITS_PURCHASE, $"Top-up balance load failed: {e.Message}");
            }
            finally
            {
                if (viewInstance != null)
                    viewInstance.BalanceLoadingSpinner.SetActive(false);
            }
        }

        private static ModalState MapStage(CreditsTopUpStage stage) =>
            stage switch
            {
                CreditsTopUpStage.CREATING_CHECKOUT => ModalState.CREATING_CHECKOUT,
                CreditsTopUpStage.WAITING_FOR_PAYMENT => ModalState.WAITING_FOR_BROWSER,
                CreditsTopUpStage.PENDING_TIMEOUT => ModalState.PENDING,
                CreditsTopUpStage.CREDITED => ModalState.SUCCESS,
                CreditsTopUpStage.FAILED => ModalState.FAILED,
                _ => ModalState.PACK_SELECTION,
            };

        private static (string reason, bool allowRetry) MapFailureCopy(in CreditsTopUpStatus status)
        {
            if (status.CheckoutError != null)
            {
                switch (status.CheckoutError.Value)
                {
                    case CreditsCheckoutError.FeatureDisabled:
                        return ("Card payments are not available right now.", false);
                    case CreditsCheckoutError.PaymentsUnavailable:
                        return ("Card payments are temporarily unavailable. Please try again later.", true);
                    case CreditsCheckoutError.UnknownPack:
                        return ("This pack is not available. Please restart the client and try again.", false);
                    default:
                        return ("Something went wrong starting your purchase. Please try again.", true);
                }
            }

            return (string.IsNullOrEmpty(status.ErrorMessage)
                ? "The payment could not be completed. Please try again."
                : status.ErrorMessage!, true);
        }

        private static string MapAnalyticsErrorCode(in CreditsTopUpStatus status) =>
            status.CheckoutError switch
            {
                CreditsCheckoutError.FeatureDisabled => "feature_disabled",
                CreditsCheckoutError.PaymentsUnavailable => "payments_unavailable",
                CreditsCheckoutError.UnknownPack => "unknown_pack",
                CreditsCheckoutError.ProviderError => "provider_error",
                CreditsCheckoutError.NetworkError => "network_error",
                CreditsCheckoutError.Cancelled => "cancelled",
                _ => ANALYTICS_ERROR_GRANT_FAILED,
            };
    }
}
