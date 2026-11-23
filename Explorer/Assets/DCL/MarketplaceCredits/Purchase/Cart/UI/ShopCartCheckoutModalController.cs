using Cysharp.Threading.Tasks;
using DCL.Backpack;
using DCL.Diagnostics;
using DCL.MarketplaceCredits.Purchase.UI;
using DCL.UI;
using MVC;
using Plugins.NativeWindowManager;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Pool;
using Utility;

namespace DCL.MarketplaceCredits.Purchase.Cart.UI
{
    /// <summary>
    ///     The cart checkout popup: the cart reviewed against its live listings, the choice between credits and
    ///     MANA, and the progress and result of the checkout.
    /// </summary>
    public class ShopCartCheckoutModalController : ControllerBase<ShopCartCheckoutModalView>
    {
        private enum ModalState
        {
            Reviewing,
            ReadyToConfirm,
            InsufficientFunds,
            Processing,
            Success,
            PartialFailure,
            Failed,
        }

        private const string TITLE_SINGULAR = "Buy 1 Item";
        private const string TITLE_FORMAT = "Buy {0} Items";
        private const string INSUFFICIENT_FUNDS_TITLE = "Insufficient Funds";
        private const string INSUFFICIENT_FUNDS_TEXT = "Your Credits and MANA are not enough for these items. Buy Credits or add MANA to continue.";
        private const string INSUFFICIENT_CREDITS_TEXT = "Your Credits are not enough for these items. Buy Credits to continue.";
        private const string MANA_ALONE_REFUSED_TEXT = "Some of these items are priced too low to be paid with MANA alone. Buy Credits to continue.";
        private const string TOTAL_SINGULAR = "Total: 1 Item";
        private const string TOTAL_FORMAT = "Total: {0} Items";
        private const string ITEM_COUNT_SINGULAR = "1 item";
        private const string ITEM_COUNT_FORMAT = "{0} items";
        private const string CONFIRMATIONS_FORMAT = "{0} confirmations needed";
        private const string RESERVING_FORMAT = "Reserving your credits ({0}/{1})...";
        private const string APPROVING_MANA = "Waiting for you to authorize MANA...";
        private const string CONFIRMING_SINGLE = "Waiting for your confirmation...";
        private const string CONFIRMING_FORMAT = "Waiting for your confirmation ({0} of {1})...";
        private const string SETTLING_SINGLE = "Completing your purchase...";
        private const string SETTLING_FORMAT = "Completing your purchase ({0} of {1})...";
        private const string CHANGES_DROPPED_FORMAT = "{0} of your items {1} no longer available and {2} removed. ";
        private const string CHANGES_TOTAL_FORMAT = "The total is now <b>{0} Credits</b>.";
        private const string SUCCESS_SUMMARY_FORMAT = "{0} added to your backpack.";
        private const string PARTIAL_SUMMARY_FORMAT = "{0} of {1} items were bought. {2}";
        private const string PENDING_SETTLEMENT_COPY = "Your purchase is still processing. What it costs is reserved and it will complete automatically — check back soon.";

        private const string ANALYTICS_STEP_REVIEW = "review";
        private const string ANALYTICS_STEP_CHECKOUT = "checkout";
        private const int UNKNOWN_BALANCE = -1;
        private static readonly TimeSpan REVIEW_TTL = TimeSpan.FromSeconds(120);

        private readonly ShopCart cart;
        private readonly CreditsCartCheckoutService checkoutService;
        private readonly ISpriteCache spriteCache;
        private readonly NftTypeIconSO rarityBackgrounds;
        private readonly Func<CancellationToken, UniTask> openGetCreditsAsync;
        private readonly Func<CancellationToken, UniTask> openBackpackAsync;
        private readonly bool isManaEnabled;
        private readonly List<ShopCartCheckoutLineView> activeLines = new ();
        private readonly CancellationTokenSource disposalCts = new ();

        private ObjectPool<ShopCartCheckoutLineView>? linePool;
        private ModalState currentState;
        private CancellationTokenSource? lifeCts;
        private CancellationTokenSource? thumbnailsCts;
        private CancellationTokenSource? reviewCts;
        private CancellationTokenSource? checkoutSessionCts;
        private CartReview? review;
        private PaymentOptions paymentOptions = PaymentOptions.NONE;
        private string buyTitle = string.Empty;

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Popup;

        public event Action<int, int, bool>? CheckoutStarted;
        public event Action<CartCheckoutResult, PaymentMethod>? CheckoutCompleted;
        public event Action<CartCheckoutResult, PaymentMethod, string, string>? CheckoutFailed;
        public event Action<CartCheckoutResult, PaymentMethod, string>? CheckoutCancelled;
        public event Action<long, int, long>? BuyCreditsPrompted;

        public ShopCartCheckoutModalController(
            ViewFactoryMethod viewFactory,
            ShopCart cart,
            CreditsCartCheckoutService checkoutService,
            ISpriteCache spriteCache,
            NftTypeIconSO rarityBackgrounds,
            Func<CancellationToken, UniTask> openGetCreditsAsync,
            Func<CancellationToken, UniTask> openBackpackAsync,
            bool isManaEnabled)
            : base(viewFactory)
        {
            this.cart = cart;
            this.checkoutService = checkoutService;
            this.spriteCache = spriteCache;
            this.rarityBackgrounds = rarityBackgrounds;
            this.openGetCreditsAsync = openGetCreditsAsync;
            this.openBackpackAsync = openBackpackAsync;
            this.isManaEnabled = isManaEnabled;
        }

        public override void Dispose()
        {
            UnsubscribeFromService();
            reviewCts.SafeCancelAndDispose();
            lifeCts.SafeCancelAndDispose();
            thumbnailsCts.SafeCancelAndDispose();
            disposalCts.SafeCancelAndDispose();

            if (viewInstance != null)
            {
                viewInstance.BuyButton.onClick.RemoveListener(OnBuyClicked);
                viewInstance.BuyCreditsButton.onClick.RemoveListener(OnBuyCreditsClicked);
                viewInstance.CancelConfirmationButton.onClick.RemoveListener(checkoutService.CancelPendingConfirmation);
                viewInstance.RetryButton.onClick.RemoveListener(StartReview);
                viewInstance.ToBackpackButton.onClick.RemoveListener(OnToBackpackClicked);
                viewInstance.PaymentSelector.SelectionChanged = null;
            }

            linePool?.Dispose();
        }

        protected override void OnViewInstantiated()
        {
            if (viewInstance == null)
                return;

            ShopCartCheckoutModalView view = viewInstance;

            linePool = new ObjectPool<ShopCartCheckoutLineView>(
                () => UnityEngine.Object.Instantiate(view.LinePrefab, view.LinesContainer),
                actionOnGet: line => line.gameObject.SetActive(true),
                actionOnRelease: line =>
                {
                    line.Unbind();
                    line.gameObject.SetActive(false);
                });

            view.BuyButton.onClick.AddListener(OnBuyClicked);
            view.BuyCreditsButton.onClick.AddListener(OnBuyCreditsClicked);
            view.CancelConfirmationButton.onClick.AddListener(checkoutService.CancelPendingConfirmation);
            view.RetryButton.onClick.AddListener(StartReview);
            view.ToBackpackButton.onClick.AddListener(OnToBackpackClicked);
            view.PaymentSelector.SelectionChanged = RenderTotal;
        }

        // The popup is reused: without this the show animation would play over the screen it was closed on.
        protected override void OnBeforeViewShow() =>
            SetUiState(ModalState.Reviewing);

        protected override void OnViewShow()
        {
            lifeCts = lifeCts.SafeRestart();
            review = null;
            paymentOptions = PaymentOptions.NONE;

            checkoutService.StateChanged += OnCheckoutProgress;
            checkoutService.CheckoutCompleted += OnCheckoutCompleted;

            if (checkoutService.IsCheckoutInFlight)
            {
                SetUiState(ModalState.Processing);
                OnCheckoutProgress(checkoutService.CurrentProgress);
                return;
            }

            CartCheckoutResult? pending = checkoutService.LastResult;

            if (pending != null)
            {
                checkoutService.AcknowledgeResult();

                // A cancelled checkout bought and held nothing, so it has nothing to show: start over.
                if (pending.Outcome != CartCheckoutOutcome.Cancelled)
                {
                    ShowResult(pending);
                    return;
                }
            }

            StartReview();
        }

        protected override void OnViewClose()
        {
            UnsubscribeFromService();
            thumbnailsCts.SafeCancelAndDispose();
            thumbnailsCts = null;
            ReleaseLines();
            reviewCts.SafeCancelAndDispose();
            reviewCts = null;
            lifeCts.SafeCancelAndDispose();
            lifeCts = null;
        }

        protected override async UniTask WaitForCloseIntentAsync(CancellationToken ct)
        {
            if (viewInstance == null)
                return;

            await UniTask.WhenAny(
                viewInstance.CloseButton.OnClickAsync(ct),
                viewInstance.CloseBackground.OnClickAsync(ct),
                viewInstance.CancelButton.OnClickAsync(ct),
                viewInstance.DoneButton.OnClickAsync(ct),
                viewInstance.ContinueShoppingButton.OnClickAsync(ct));
        }

        private void UnsubscribeFromService()
        {
            checkoutService.StateChanged -= OnCheckoutProgress;
            checkoutService.CheckoutCompleted -= OnCheckoutCompleted;
        }

        private void StartReview()
        {
            if (lifeCts == null || lifeCts.IsCancellationRequested)
                return;

            ReviewAsync(lifeCts.Token).Forget();
        }

        private async UniTaskVoid ReviewAsync(CancellationToken ct)
        {
            SetUiState(ModalState.Reviewing);

            // Each review runs on its own token, so a newer one drops the answer of the one before it.
            reviewCts = reviewCts.SafeRestartLinked(ct);
            CancellationToken reviewCt = reviewCts.Token;

            try
            {
                // The review works on a copy: the cart is edited by a checkout that completes meanwhile.
                CartReviewResult reviewResult = await checkoutService.ReviewAsync(new List<ShopCartLine>(cart.Lines), reviewCt);

                if (reviewCt.IsCancellationRequested)
                    return;

                if (!reviewResult.Success || reviewResult.Review == null)
                {
                    ShowFailure(ErrorCopy(reviewResult.Error), allowRetry: reviewResult.Error != CreditsPurchaseError.FeatureDisabled);
                    return;
                }

                CartReview reviewed = reviewResult.Review;
                PaymentOptions options = await checkoutService.GetPaymentOptionsAsync(reviewed, reviewCt);

                if (reviewCt.IsCancellationRequested)
                    return;

                review = reviewed;
                paymentOptions = options;
                int balanceCredits = BalanceCredits(options);

                CheckoutStarted?.Invoke(cart.TotalUnits, reviewed.LiveTotalCredits, balanceCredits >= reviewed.LiveTotalCredits);

                if (reviewed.Buyable.Count == 0)
                {
                    ShowFailure("None of the items in your cart can be bought right now.", allowRetry: false);
                    return;
                }

                bool canPay = options.Options.Count > 0;

                // Nothing payable and no credits balance means the balance read failed, not that the buyer is short.
                if (!canPay && options.CreditsBalanceCents == null)
                {
                    ShowFailure("Could not load your balance.", allowRetry: true);
                    return;
                }

                RenderReview(reviewed, options);
                SetUiState(canPay ? ModalState.ReadyToConfirm : ModalState.InsufficientFunds);

                if (!canPay)
                    BuyCreditsPrompted?.Invoke(reviewed.LiveTotalCredits, balanceCredits, Math.Max(1L, reviewed.LiveTotalCredits - Math.Max(0, balanceCredits)));
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                ReportHub.LogException(e, new ReportData(ReportCategory.CREDITS_PURCHASE));

                if (!reviewCt.IsCancellationRequested)
                    ShowFailure("Something went wrong. Please try again.", allowRetry: true);
            }
        }

        private void RenderReview(CartReview reviewed, PaymentOptions options)
        {
            if (viewInstance == null)
                return;

            buyTitle = reviewed.UnitCount == 1 ? TITLE_SINGULAR : string.Format(TITLE_FORMAT, reviewed.UnitCount);
            viewInstance.TotalItemsText.text = reviewed.UnitCount == 1 ? TOTAL_SINGULAR : string.Format(TOTAL_FORMAT, reviewed.UnitCount);
            viewInstance.InsufficientText.text = InsufficientCopy(options);

            viewInstance.BalanceCreditsText.text = options.CreditsBalanceCents is { } balanceCents
                ? PaymentAmountFormat.Credits(balanceCents / PaymentOptionsCalculator.CENTS_PER_CREDIT)
                : PaymentAmountFormat.UNKNOWN;

            viewInstance.BalanceManaGroup.SetActive(isManaEnabled);

            viewInstance.BalanceManaText.text = options.ManaBalanceWei is { } balanceWei
                ? PaymentAmountFormat.ManaBalance(balanceWei)
                : PaymentAmountFormat.UNKNOWN;

            viewInstance.ChangesNoticeText.gameObject.SetActive(reviewed.OrderChanged);

            if (reviewed.OrderChanged)
                viewInstance.ChangesNoticeText.text = ChangesNotice(reviewed);

            RenderLines(reviewed);
            viewInstance.PaymentSelector.Show(options, isManaEnabled);
            RenderTotal();
        }

        private string InsufficientCopy(PaymentOptions options)
        {
            if (!isManaEnabled)
                return INSUFFICIENT_CREDITS_TEXT;

            return options.IsManaAloneRefused ? MANA_ALONE_REFUSED_TEXT : INSUFFICIENT_FUNDS_TEXT;
        }

        private static string ChangesNotice(CartReview reviewed)
        {
            int dropped = reviewed.Dropped.Count;

            string droppedCopy = dropped > 0
                ? string.Format(CHANGES_DROPPED_FORMAT, dropped, dropped == 1 ? "is" : "are", dropped == 1 ? "was" : "were")
                : string.Empty;

            return droppedCopy + string.Format(CHANGES_TOTAL_FORMAT, PaymentAmountFormat.Credits(reviewed.LiveTotalCredits));
        }

        private void RenderLines(CartReview reviewed)
        {
            if (viewInstance == null || linePool == null)
                return;

            thumbnailsCts = thumbnailsCts.SafeRestart();

            for (var i = 0; i < reviewed.Buyable.Count; i++)
            {
                ReviewedCartLine line = reviewed.Buyable[i];
                ShopCartCheckoutLineView lineView;

                if (i < activeLines.Count)
                    lineView = activeLines[i];
                else
                {
                    lineView = linePool.Get();
                    lineView.transform.SetAsLastSibling();
                    activeLines.Add(lineView);
                }

                string thumbnailUrl = line.Line.Listing.thumbnail;
                Sprite? cached = spriteCache.GetCachedSprite(thumbnailUrl);
                lineView.Bind(line, cached, rarityBackgrounds.GetTypeImage(line.Line.Listing.rarity));

                if (cached == null)
                    LoadThumbnailAsync(lineView, thumbnailUrl, thumbnailsCts.Token).Forget();
            }

            for (int i = activeLines.Count - 1; i >= reviewed.Buyable.Count; i--)
            {
                linePool.Release(activeLines[i]);
                activeLines.RemoveAt(i);
            }

            viewInstance.SetVisibleLines(reviewed.Buyable.Count);
        }

        private void ReleaseLines()
        {
            if (linePool == null)
                return;

            foreach (ShopCartCheckoutLineView line in activeLines)
                linePool.Release(line);

            activeLines.Clear();
        }

        private async UniTaskVoid LoadThumbnailAsync(ShopCartCheckoutLineView lineView, string url, CancellationToken ct)
        {
            string? boundId = lineView.BoundLineId;

            try
            {
                Sprite? sprite = await spriteCache.GetSpriteAsync(url, ct: ct);

                if (!ct.IsCancellationRequested && sprite != null && lineView.BoundLineId == boundId)
                    lineView.SetThumbnail(sprite);
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                ReportHub.LogWarning(ReportCategory.CREDITS_PURCHASE, $"Checkout thumbnail failed for {url}: {e.Message}");
            }
        }

        // The total is what the ticked rows charge; with nothing payable it is the price in credits.
        private void RenderTotal()
        {
            if (viewInstance == null || review == null)
                return;

            bool hasSelection = viewInstance.PaymentSelector.Selection.TryGetSelected(out PaymentOption option);
            bool paysCredits = !hasSelection || option.Method != PaymentMethod.Mana;
            bool paysMana = hasSelection && option.Method != PaymentMethod.Credits;

            viewInstance.TotalCreditsGroup.SetActive(paysCredits);
            viewInstance.TotalManaGroup.SetActive(paysMana);
            viewInstance.TotalPlus.SetActive(paysCredits && paysMana);
            viewInstance.TotalCreditsText.text = PaymentAmountFormat.Credits(hasSelection ? option.Credits : review.LiveTotalCredits);

            if (paysMana)
                viewInstance.TotalManaText.text = PaymentAmountFormat.ManaPrice(option.ManaWei);
        }

        private void OnBuyClicked()
        {
            if (currentState != ModalState.ReadyToConfirm || review == null || viewInstance == null || lifeCts == null || lifeCts.IsCancellationRequested)
                return;

            // A stale review is redone and shown again: its prices are what the buyer confirms.
            if (DateTime.UtcNow - review.ReviewedAtUtc > REVIEW_TTL)
            {
                StartReview();
                return;
            }

            if (!viewInstance.PaymentSelector.Selection.TryGetSelected(out PaymentOption option))
                return;

            CheckoutAsync(review, option, lifeCts.Token).Forget();
        }

        private async UniTaskVoid CheckoutAsync(CartReview reviewed, PaymentOption option, CancellationToken ct)
        {
            SetUiState(ModalState.Processing);
            NativeWindowManager.RequestTemporaryWindowMode();
            checkoutSessionCts = lifeCts;

            try
            {
                CartCheckoutResult result = await checkoutService.CheckoutAsync(reviewed, option, ct);
                ReportResult(result, option.Method, reviewed);

                // A popup closed during the checkout leaves the result on the service for the next time it opens.
                if (ct.IsCancellationRequested)
                    return;

                checkoutService.AcknowledgeResult();
                ShowResult(result);
            }
            catch (Exception e)
            {
                ReportHub.LogException(e, new ReportData(ReportCategory.CREDITS_PURCHASE));

                if (!ct.IsCancellationRequested)
                    ShowFailure("Something went wrong. Please try again.", allowRetry: true);
            }
            finally
            {
                checkoutSessionCts = null;
                NativeWindowManager.ReleaseTemporaryWindowMode();
            }
        }

        // Renders a checkout this popup session did not start: one that was still running when the popup was reopened.
        private void OnCheckoutCompleted(CartCheckoutResult result)
        {
            if (currentState != ModalState.Processing || checkoutSessionCts == lifeCts)
                return;

            checkoutService.AcknowledgeResult();
            ShowResult(result);
        }

        private void ReportResult(CartCheckoutResult result, PaymentMethod method, CartReview reviewed)
        {
            switch (result.Outcome)
            {
                case CartCheckoutOutcome.Completed:
                case CartCheckoutOutcome.PartiallyCompleted:
                    CheckoutCompleted?.Invoke(result, method);
                    break;
                case CartCheckoutOutcome.InsufficientCredits:
                    int balanceCredits = BalanceCredits(paymentOptions);
                    long needed = reviewed.LiveTotalCredits;
                    long shortfall = result.MissingCredits >= 0 ? result.MissingCredits : Math.Max(1L, needed - Math.Max(0, balanceCredits));
                    BuyCreditsPrompted?.Invoke(needed, balanceCredits, shortfall);
                    break;
                case CartCheckoutOutcome.Cancelled:
                    CheckoutCancelled?.Invoke(result, method, MapAnalyticsStep(result.FailedAtStage));
                    break;
                default:
                    CheckoutFailed?.Invoke(result, method, MapAnalyticsStep(result.FailedAtStage), CreditPurchaseModalController.MapAnalyticsErrorCode(result.FirstError));
                    break;
            }
        }

        private void ShowResult(CartCheckoutResult result)
        {
            switch (result.Outcome)
            {
                case CartCheckoutOutcome.Completed:
                    SetUiState(ModalState.Success);

                    if (viewInstance != null)
                        viewInstance.SuccessSummaryText.text = string.Format(SUCCESS_SUMMARY_FORMAT, FormatItemCount(result.BoughtUnits.Count));

                    break;
                case CartCheckoutOutcome.PartiallyCompleted:
                    SetUiState(ModalState.PartialFailure);

                    if (viewInstance != null)
                    {
                        int total = result.BoughtUnits.Count + result.UnboughtUnits.Count;
                        string reason = result.HasPendingSettlement ? PENDING_SETTLEMENT_COPY : ErrorCopy(result.FirstError);
                        viewInstance.PartialSummaryText.text = string.Format(PARTIAL_SUMMARY_FORMAT, result.BoughtUnits.Count, total, reason);
                    }

                    break;
                case CartCheckoutOutcome.Cancelled:
                    // Nothing was bought or held: back to the confirmation the buyer left, when there is one.
                    if (review != null)
                        SetUiState(ModalState.ReadyToConfirm);
                    else
                        StartReview();

                    break;
                default:
                    ShowFailure(result.HasPendingSettlement ? PENDING_SETTLEMENT_COPY : ErrorCopy(result.FirstError), allowRetry: !result.HasPendingSettlement);
                    break;
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

        private void OnCheckoutProgress(CartCheckoutProgress progress)
        {
            if (viewInstance == null || currentState != ModalState.Processing)
                return;

            bool many = progress.GroupCount > 1;

            viewInstance.ProgressStatusText.text = progress.Stage switch
                                                   {
                                                       CartCheckoutStage.Reserving => string.Format(RESERVING_FORMAT, progress.UnitsReserved, progress.UnitCount),
                                                       CartCheckoutStage.ApprovingMana => APPROVING_MANA,
                                                       CartCheckoutStage.Signing => many ? string.Format(CONFIRMING_FORMAT, progress.GroupIndex, progress.GroupCount) : CONFIRMING_SINGLE,
                                                       CartCheckoutStage.WaitingSettlement => many ? string.Format(SETTLING_FORMAT, progress.GroupIndex, progress.GroupCount) : SETTLING_SINGLE,
                                                       _ => viewInstance.ProgressStatusText.text,
                                                   };

            viewInstance.CancelConfirmationButton.gameObject.SetActive(progress.Stage is CartCheckoutStage.Signing or CartCheckoutStage.ApprovingMana);
            viewInstance.SignatureCountBadge.SetActive(many);

            if (many)
                viewInstance.SignatureCountText.text = string.Format(CONFIRMATIONS_FORMAT, progress.GroupCount);
        }

        private void OnBuyCreditsClicked()
        {
            RequestClose();
            NavigateAfterCloseAsync(openGetCreditsAsync, disposalCts.Token).Forget();
        }

        private void OnToBackpackClicked()
        {
            RequestClose();
            NavigateAfterCloseAsync(openBackpackAsync, disposalCts.Token).Forget();
        }

        private static async UniTaskVoid NavigateAfterCloseAsync(Func<CancellationToken, UniTask> navigate, CancellationToken ct)
        {
            try { await navigate(ct); }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                ReportHub.LogException(e, new ReportData(ReportCategory.CREDITS_PURCHASE));
            }
        }

        private void SetUiState(ModalState newState)
        {
            currentState = newState;

            if (viewInstance == null)
                return;

            bool insufficient = newState == ModalState.InsufficientFunds;
            bool confirming = newState == ModalState.ReadyToConfirm || insufficient;

            viewInstance.ReviewingContainer.SetActive(newState == ModalState.Reviewing);
            viewInstance.ConfirmStateContainer.SetActive(confirming);
            viewInstance.ProgressStateContainer.SetActive(newState == ModalState.Processing);
            viewInstance.SuccessStateContainer.SetActive(newState == ModalState.Success);
            viewInstance.PartialFailureStateContainer.SetActive(newState == ModalState.PartialFailure);
            viewInstance.FailedStateContainer.SetActive(newState == ModalState.Failed);

            viewInstance.TitleText.text = insufficient ? INSUFFICIENT_FUNDS_TITLE : buyTitle;
            viewInstance.InsufficientIcon.SetActive(insufficient);
            viewInstance.InsufficientText.gameObject.SetActive(insufficient);
            viewInstance.BuyButton.gameObject.SetActive(newState == ModalState.ReadyToConfirm);
            viewInstance.BuyCreditsButton.gameObject.SetActive(insufficient);

            // The confirmation has its own Cancel button.
            viewInstance.CloseButton.gameObject.SetActive(!confirming);

            if (newState != ModalState.Processing)
            {
                viewInstance.CancelConfirmationButton.gameObject.SetActive(false);
                viewInstance.SignatureCountBadge.SetActive(false);
            }
        }

        private void RequestClose() =>
            viewInstance?.CloseButton.onClick.Invoke();

        private static int BalanceCredits(PaymentOptions options) =>
            options.CreditsBalanceCents is { } balanceCents
                ? (int)Math.Min(int.MaxValue, balanceCents / PaymentOptionsCalculator.CENTS_PER_CREDIT)
                : UNKNOWN_BALANCE;

        private static string FormatItemCount(int count) =>
            count == 1 ? ITEM_COUNT_SINGULAR : string.Format(ITEM_COUNT_FORMAT, count);

        private static string MapAnalyticsStep(CartCheckoutStage stage) =>
            stage switch
            {
                CartCheckoutStage.Reviewing => ANALYTICS_STEP_REVIEW,
                CartCheckoutStage.Reserving => "reserving",
                CartCheckoutStage.ApprovingMana => "approving_mana",
                CartCheckoutStage.Signing => "signing",
                CartCheckoutStage.WaitingSettlement => "waiting_settlement",
                _ => ANALYTICS_STEP_CHECKOUT,
            };

        // Web2 copy: no wallet, signature, chain or transaction wording.
        private static string ErrorCopy(CreditsPurchaseError error) =>
            error switch
            {
                CreditsPurchaseError.InsufficientCredits => "You don't have enough Credits for this purchase.",
                CreditsPurchaseError.InsufficientMana => "You don't have enough MANA for this purchase.",
                CreditsPurchaseError.ManaApprovalFailed => "MANA could not be authorized for this purchase. Nothing was spent.",
                CreditsPurchaseError.SettlementPending => PENDING_SETTLEMENT_COPY,
                CreditsPurchaseError.SignatureRejected or CreditsPurchaseError.Cancelled => "You cancelled the request.",
                CreditsPurchaseError.SignatureTimedOut => "The confirmation timed out. Nothing was spent.",
                CreditsPurchaseError.PriceChanged => "The price of an item changed. Please review your cart and try again.",
                CreditsPurchaseError.PriceUnavailable => "A price is unavailable right now. Please try again later.",
                CreditsPurchaseError.ListingNotAvailable => "Some items are no longer available for purchase.",
                CreditsPurchaseError.OwnListing => "You cannot buy your own listing.",
                CreditsPurchaseError.TransactionReverted => "The purchase failed. Nothing was spent.",
                CreditsPurchaseError.RelayerUnavailable => "The purchase service is temporarily unavailable. Please try again later.",
                CreditsPurchaseError.FeatureDisabled => "Buying items is not available right now.",
                CreditsPurchaseError.PurchaseInProgress => "Another purchase is still processing. Wait for it to finish and try again.",
                _ => "The purchase failed. Nothing was spent.",
            };
    }
}
