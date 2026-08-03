using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.UI;
using DCL.Web3.Identities;
using MVC;
using System;
using System.Globalization;
using System.Threading;
using Utility;

namespace DCL.MarketplaceCredits.Purchase.TopUp.UI
{
    public class CreditsTopUpModalController : ControllerBase<CreditsTopUpModalView, CreditsTopUpModalControllerParams>
    {
        private enum ModalState
        {
            PackSelection,
            CreatingCheckout,
            WaitingForBrowser,
            Pending,
            Success,
            Failed,
        }

        private const string ANALYTICS_STEP_CHECKOUT = "checkout";
        private const string ANALYTICS_STEP_GRANT = "grant";
        private const string ANALYTICS_ERROR_GRANT_FAILED = "grant_failed";

        private readonly ICreditsTopUpService topUpService;
        private readonly MarketplaceCreditsAPIClient creditsAPIClient;
        private readonly IWeb3IdentityCache identityCache;
        private readonly ImageControllerProvider imageControllerProvider;

        private ModalState currentState;
        private CreditsTopUpStage lastStage = CreditsTopUpStage.Idle;
        private bool isViewShown;
        private CancellationTokenSource? lifeCts;

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Popup;

        public event Action<CreditPack, string>? BuyCreditsStarted;
        public event Action<string, CreditPack>? RedirectedToStripe;
        public event Action<string, CreditPack>? BuyCreditsCompleted;
        public event Action<CreditPack>? BuyCreditsPending;
        public event Action<string, string, CreditPack>? BuyCreditsFailed;

        public CreditsTopUpModalController(
            ViewFactoryMethod viewFactory,
            ICreditsTopUpService topUpService,
            MarketplaceCreditsAPIClient creditsAPIClient,
            IWeb3IdentityCache identityCache,
            ImageControllerProvider imageControllerProvider)
            : base(viewFactory)
        {
            this.topUpService = topUpService;
            this.creditsAPIClient = creditsAPIClient;
            this.identityCache = identityCache;
            this.imageControllerProvider = imageControllerProvider;
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
                viewInstance.RetryButton.onClick.AddListener(OnRetryClicked);

            ApplyStatus(topUpService.CurrentStatus);
            LoadAndBindPacksAsync(lifeCts.Token).Forget();
            LoadBalanceAsync(lifeCts.Token).Forget();
        }

        protected override void OnViewClose()
        {
            isViewShown = false;

            if (viewInstance != null)
            {
                foreach (CreditsTopUpPackItemView packItem in viewInstance.PackItems)
                {
                    packItem.BuyButton.onClick.RemoveAllListeners();
                    packItem.StopLoadingImage();
                }

                viewInstance.RetryButton.onClick.RemoveListener(OnRetryClicked);
            }

            if (currentState == ModalState.WaitingForBrowser)
                topUpService.StopWaitingForBrowser();
            else if (currentState is ModalState.Success or ModalState.Failed or ModalState.Pending)
                topUpService.AcknowledgeTerminalState();

            lifeCts.SafeCancelAndDispose();
        }

        protected override async UniTask WaitForCloseIntentAsync(CancellationToken ct)
        {
            if (viewInstance == null)
                return;

            await UniTask.WhenAny(viewInstance.CloseButton.OnClickAsync(ct),
                viewInstance.DoneButton.OnClickAsync(ct));
        }

        private async UniTaskVoid LoadAndBindPacksAsync(CancellationToken ct)
        {
            if (viewInstance == null)
                return;

            viewInstance.PacksLoadingSpinner.SetActive(true);
            viewInstance.PacksErrorContainer.SetActive(false);
            HideAllPackItems();

            CreditPacksResponse response;

            try { response = await creditsAPIClient.GetCreditPacksAsync(ct); }
            catch (OperationCanceledException) { return; }
            catch (Exception e)
            {
                ReportHub.LogWarning(ReportCategory.CREDITS_PURCHASE, $"Top-up packs load failed: {e.Message}");
                ShowPacksError();
                return;
            }

            if (ct.IsCancellationRequested || viewInstance == null)
                return;

            if (response.packs == null || response.packs.Length == 0)
            {
                ShowPacksError();
                return;
            }

            viewInstance.PacksLoadingSpinner.SetActive(false);
            viewInstance.PacksErrorContainer.SetActive(false);
            BindPackItems(response.packs);
        }

        private void BindPackItems(CreditPackData[] packsData)
        {
            if (viewInstance == null)
                return;

            Array.Sort(packsData, static (a, b) => a.order.CompareTo(b.order));

            CreditsTopUpPackItemView[] slots = viewInstance.PackItems;
            int count = Math.Min(slots.Length, packsData.Length);

            for (var i = 0; i < slots.Length; i++)
            {
                CreditsTopUpPackItemView packItem = slots[i];

                if (i >= count)
                {
                    packItem.gameObject.SetActive(false);
                    continue;
                }

                CreditPack pack = ToCreditPack(packsData[i]);

                packItem.PriceText.text = $"${pack.PriceUsd.ToString("0.##", CultureInfo.InvariantCulture)}";
                packItem.CreditsText.text = pack.Credits.ToString();
                packItem.BestValueBadge.SetActive(pack.BestValue);

                packItem.BuyButton.onClick.RemoveAllListeners();
                packItem.BuyButton.onClick.AddListener(() => OnPackClicked(pack));

                packItem.ConfigureImageController(imageControllerProvider);
                packItem.SetupImage(pack.ImageUrl);

                packItem.gameObject.SetActive(true);
            }

            if (packsData.Length > slots.Length)
                ReportHub.LogWarning(ReportCategory.CREDITS_PURCHASE,
                    $"Server returned {packsData.Length} credit packs but only {slots.Length} UI slots exist; extra packs are not shown.");
        }

        private void HideAllPackItems()
        {
            if (viewInstance == null)
                return;

            foreach (CreditsTopUpPackItemView packItem in viewInstance.PackItems)
                packItem.gameObject.SetActive(false);
        }

        private void ShowPacksError()
        {
            if (viewInstance == null)
                return;

            viewInstance.PacksLoadingSpinner.SetActive(false);
            viewInstance.PacksErrorContainer.SetActive(true);
            HideAllPackItems();
        }

        private static CreditPack ToCreditPack(in CreditPackData data) =>
            new (data.id, data.usd, data.credits, data.recommended, data.imageUrl);

        private void OnPackClicked(CreditPack pack)
        {
            if (currentState != ModalState.PackSelection || topUpService.IsOrderInFlight)
                return;

            BuyCreditsStarted?.Invoke(pack, inputData.Source);
            topUpService.StartTopUp(pack);
        }

        private void OnRetryClicked()
        {
            if (currentState != ModalState.Failed)
                return;

            topUpService.AcknowledgeTerminalState();
        }

        private void OnServiceStatusChanged(CreditsTopUpStatus status)
        {
            if (status.Stage != lastStage)
            {
                switch (status.Stage)
                {
                    case CreditsTopUpStage.WaitingForPayment:
                        RedirectedToStripe?.Invoke(status.OrderId!, status.Pack);
                        break;
                    case CreditsTopUpStage.PendingTimeout:
                        BuyCreditsPending?.Invoke(status.Pack);
                        break;
                    case CreditsTopUpStage.Credited:
                        BuyCreditsCompleted?.Invoke(status.OrderId!, status.Pack);
                        break;
                    case CreditsTopUpStage.Failed:
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
                case CreditsTopUpStage.Credited:
                    viewInstance.BoughtCreditsAmount.text = status.Pack.Credits.ToString();
                    viewInstance.BalanceCreditsText.text = status.NewBalance.ToString();
                    break;
                case CreditsTopUpStage.Failed:
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

            bool packsVisible = newState is ModalState.PackSelection or ModalState.CreatingCheckout;
            bool fullScreenState = newState is ModalState.WaitingForBrowser or ModalState.Success;

            viewInstance.HeaderContainer.SetActive(!fullScreenState);
            viewInstance.BalanceContainer.SetActive(!fullScreenState);

            viewInstance.PackSelectionContainer.SetActive(packsVisible);
            viewInstance.WaitingForBrowserContainer.SetActive(newState == ModalState.WaitingForBrowser);
            viewInstance.FailedContainer.SetActive(newState == ModalState.Failed);
            viewInstance.SuccessContainer.SetActive(newState == ModalState.Success);

            foreach (CreditsTopUpPackItemView packItem in viewInstance.PackItems)
                packItem.BuyButton.interactable = newState == ModalState.PackSelection;

            // Close is locked only while creating the checkout; during the browser wait the X acts as
            // "stop waiting" and hands the order off to the background poll.
            viewInstance.CloseButton.interactable = newState != ModalState.CreatingCheckout;
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
                    viewInstance.BalanceCreditsText.text = credits.usd.credits.ToString();
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
                CreditsTopUpStage.CreatingCheckout => ModalState.CreatingCheckout,
                CreditsTopUpStage.WaitingForPayment => ModalState.WaitingForBrowser,
                CreditsTopUpStage.PendingTimeout => ModalState.Pending,
                CreditsTopUpStage.Credited => ModalState.Success,
                CreditsTopUpStage.Failed => ModalState.Failed,
                _ => ModalState.PackSelection,
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
