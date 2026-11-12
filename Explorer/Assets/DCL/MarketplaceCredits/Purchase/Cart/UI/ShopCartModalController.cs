using Cysharp.Threading.Tasks;
using DCL.Backpack;
using DCL.Diagnostics;
using DCL.MarketplaceCredits.Purchase.UI;
using DCL.UI;
using DCL.Web3.Identities;
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
    ///     The cart popup: the lines with their quantities, the live total against the balance, and the checkout
    ///     that reviews, reserves, signs and settles the whole cart through <see cref="CreditsCartCheckoutService" />.
    /// </summary>
    public class ShopCartModalController : ControllerBase<ShopCartModalView, ShopCartModalParams>
    {
        private enum ModalState
        {
            Lines,
            Reviewing,
            ConfirmChanges,
            InsufficientCredits,
            Processing,
            Success,
            PartialFailure,
            Failed,
        }

        private const float ANCHOR_GAP = 8f;
        private const string TITLE_FORMAT = "Your Cart ({0})";
        private const string ITEM_COUNT_SINGULAR = "1 item";
        private const string ITEM_COUNT_FORMAT = "{0} items";
        private const string CONFIRMATIONS_FORMAT = "{0} confirmations needed";
        private const string SHORTFALL_FORMAT = "Add <b>{0} Credits</b> to complete your purchase.";
        private const string RESERVING_FORMAT = "Reserving your credits ({0}/{1})...";
        private const string CONFIRMING_SINGLE = "Waiting for your confirmation...";
        private const string CONFIRMING_FORMAT = "Waiting for your confirmation ({0} of {1})...";
        private const string SETTLING_SINGLE = "Completing your purchase...";
        private const string SETTLING_FORMAT = "Completing your purchase ({0} of {1})...";
        private const string CHANGES_DROPPED_FORMAT = "{0} of your items {1} no longer available and {2} removed. ";
        private const string CHANGES_TOTAL_FORMAT = "The total is now <b>{0} Credits</b>.";
        private const string SUCCESS_SUMMARY_FORMAT = "{0} added to your backpack.";
        private const string PARTIAL_SUMMARY_FORMAT = "{0} of {1} items were bought. {2}";
        private const string PENDING_SETTLEMENT_COPY = "Your purchase is still processing. Your credits are reserved and it will complete automatically — check back soon.";

        private const string ANALYTICS_STEP_REVIEW = "review";
        private const string ANALYTICS_STEP_CHECKOUT = "checkout";
        private static readonly TimeSpan REVIEW_TTL = TimeSpan.FromSeconds(120);

        private readonly ShopCart cart;
        private readonly CreditsCartCheckoutService checkoutService;
        private readonly MarketplaceCreditsAPIClient creditsApiClient;
        private readonly IWeb3IdentityCache identityCache;
        private readonly ISpriteCache spriteCache;
        private readonly NftTypeIconSO rarityBackgrounds;
        private readonly Func<CancellationToken, UniTask> openGetCreditsAsync;
        private readonly Func<CancellationToken, UniTask> openBackpackAsync;
        private readonly List<ShopCartLineView> activeLines = new ();
        private readonly CancellationTokenSource disposalCts = new ();

        private ObjectPool<ShopCartLineView>? linePool;
        private ModalState currentState;
        private CancellationTokenSource? lifeCts;
        private CancellationTokenSource? thumbnailsCts;
        private CartReview? review;
        private IShopCartFavorites? favorites;
        private int balanceCredits = -1;
        private bool awaitingOwnCheckout;

        public ShopCart Cart => cart;

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Popup;

        public event Action<string, int, long>? CartOpened;
        public event Action<int, int, bool>? CheckoutStarted;
        public event Action<CartCheckoutResult>? CheckoutCompleted;
        public event Action<CartCheckoutResult, string, string>? CheckoutFailed;
        public event Action<CartCheckoutResult, string>? CheckoutCancelled;
        public event Action<long, int, long>? BuyCreditsPrompted;
        public event Action<ShopCartLine>? LineRemoved;

        public ShopCartModalController(
            ViewFactoryMethod viewFactory,
            ShopCart cart,
            CreditsCartCheckoutService checkoutService,
            MarketplaceCreditsAPIClient creditsApiClient,
            IWeb3IdentityCache identityCache,
            ISpriteCache spriteCache,
            NftTypeIconSO rarityBackgrounds,
            Func<CancellationToken, UniTask> openGetCreditsAsync,
            Func<CancellationToken, UniTask> openBackpackAsync)
            : base(viewFactory)
        {
            this.cart = cart;
            this.checkoutService = checkoutService;
            this.creditsApiClient = creditsApiClient;
            this.identityCache = identityCache;
            this.spriteCache = spriteCache;
            this.rarityBackgrounds = rarityBackgrounds;
            this.openGetCreditsAsync = openGetCreditsAsync;
            this.openBackpackAsync = openBackpackAsync;
        }

        public override void Dispose()
        {
            UnsubscribeFromServices();
            lifeCts.SafeCancelAndDispose();
            thumbnailsCts.SafeCancelAndDispose();
            disposalCts.SafeCancelAndDispose();

            if (viewInstance != null)
            {
                viewInstance.CheckoutButton.onClick.RemoveListener(OnCheckoutClicked);
                viewInstance.ConfirmChangesButton.onClick.RemoveListener(OnConfirmChangesClicked);
                viewInstance.BackToCartButton.onClick.RemoveListener(ShowLines);
                viewInstance.CancelConfirmationButton.onClick.RemoveListener(checkoutService.CancelPendingConfirmation);
                viewInstance.RetryButton.onClick.RemoveListener(ShowLines);
                viewInstance.BuyCreditsButton.onClick.RemoveListener(OnBuyCreditsClicked);
                viewInstance.ToBackpackButton.onClick.RemoveListener(OnToBackpackClicked);
            }

            linePool?.Dispose();
        }

        protected override void OnViewInstantiated()
        {
            linePool = new ObjectPool<ShopCartLineView>(
                CreateLineView,
                actionOnGet: line => line.gameObject.SetActive(true),
                actionOnRelease: line =>
                {
                    line.Unbind();
                    line.gameObject.SetActive(false);
                });

            viewInstance!.CheckoutButton.onClick.AddListener(OnCheckoutClicked);
            viewInstance.ConfirmChangesButton.onClick.AddListener(OnConfirmChangesClicked);
            viewInstance.BackToCartButton.onClick.AddListener(ShowLines);
            viewInstance.CancelConfirmationButton.onClick.AddListener(checkoutService.CancelPendingConfirmation);
            viewInstance.RetryButton.onClick.AddListener(ShowLines);
            viewInstance.BuyCreditsButton.onClick.AddListener(OnBuyCreditsClicked);
            viewInstance.ToBackpackButton.onClick.AddListener(OnToBackpackClicked);
        }

        protected override void OnBeforeViewShow() =>
            viewInstance!.PositionBelow(inputData.AnchorBottomRight, ANCHOR_GAP);

        protected override void OnViewShow()
        {
            lifeCts = lifeCts.SafeRestart();
            review = null;

            cart.Changed += OnCartChanged;
            checkoutService.StateChanged += OnCheckoutProgress;
            checkoutService.CheckoutCompleted += OnCheckoutCompleted;
            favorites = inputData.Favorites;
            favorites.FavoritesChanged += OnFavoritesChanged;

            CartOpened?.Invoke(inputData.Source, cart.TotalUnits, cart.TotalCredits);

            if (checkoutService.IsCheckoutInFlight)
            {
                SetUiState(ModalState.Processing);
                OnCheckoutProgress(checkoutService.CurrentProgress);
            }
            else if (checkoutService.LastResult != null)
            {
                CartCheckoutResult pending = checkoutService.LastResult;
                checkoutService.AcknowledgeResult();
                ShowResult(pending);
            }
            else
                ShowLines();

            LoadBalanceAsync(lifeCts.Token).Forget();
        }

        protected override void OnViewClose()
        {
            UnsubscribeFromServices();
            thumbnailsCts.SafeCancelAndDispose();
            thumbnailsCts = null;
            ReleaseLines();
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
                viewInstance.DoneButton.OnClickAsync(ct),
                viewInstance.ContinueShoppingButton.OnClickAsync(ct));
        }

        private void UnsubscribeFromServices()
        {
            cart.Changed -= OnCartChanged;
            checkoutService.StateChanged -= OnCheckoutProgress;
            checkoutService.CheckoutCompleted -= OnCheckoutCompleted;

            if (favorites != null)
            {
                favorites.FavoritesChanged -= OnFavoritesChanged;
                favorites = null;
            }
        }

        private ShopCartLineView CreateLineView()
        {
            ShopCartLineView line = UnityEngine.Object.Instantiate(viewInstance!.LinePrefab, viewInstance.LinesContainer);
            line.IncrementClicked = OnIncrementClicked;
            line.DecrementClicked = OnDecrementClicked;
            line.RemoveClicked = OnRemoveClicked;
            line.FavoriteClicked = OnFavoriteClicked;
            return line;
        }

        private void ShowLines()
        {
            review = null;
            SetUiState(ModalState.Lines);
            RenderLines();
        }

        private void RenderLines()
        {
            if (viewInstance == null || linePool == null)
                return;

            thumbnailsCts = thumbnailsCts.SafeRestart();

            // Rows are rebound in place: a row that stays under the pointer keeps its hover buttons.
            for (var i = 0; i < cart.Lines.Count; i++)
            {
                ShopCartLine line = cart.Lines[i];
                ShopCartLineView lineView;

                if (i < activeLines.Count)
                    lineView = activeLines[i];
                else
                {
                    lineView = linePool.Get();
                    lineView.transform.SetAsLastSibling();
                    activeLines.Add(lineView);
                }

                Sprite? cached = spriteCache.GetCachedSprite(line.Listing.thumbnail);
                lineView.Bind(line, cached, rarityBackgrounds.GetTypeImage(line.Listing.rarity));
                RefreshFavorite(lineView, line);

                if (cached == null)
                    LoadThumbnailAsync(lineView, line.Listing.thumbnail, thumbnailsCts.Token).Forget();
            }

            for (int i = activeLines.Count - 1; i >= cart.Lines.Count; i--)
            {
                linePool.Release(activeLines[i]);
                activeLines.RemoveAt(i);
            }

            viewInstance.EmptyState.SetActive(cart.Count == 0);
            viewInstance.CheckoutButton.interactable = cart.Count > 0;
            viewInstance.TitleText.text = string.Format(TITLE_FORMAT, cart.TotalUnits);
            viewInstance.TotalCreditsText.text = cart.TotalCredits.ToString();
        }

        private void ReleaseLines()
        {
            if (linePool == null)
                return;

            foreach (ShopCartLineView line in activeLines)
                linePool.Release(line);

            activeLines.Clear();
        }

        private async UniTaskVoid LoadThumbnailAsync(ShopCartLineView lineView, string url, CancellationToken ct)
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
                ReportHub.LogWarning(ReportCategory.CREDITS_PURCHASE, $"Cart thumbnail failed for {url}: {e.Message}");
            }
        }

        private void OnCartChanged()
        {
            switch (currentState)
            {
                case ModalState.Lines:
                    RenderLines();
                    break;
                case ModalState.Reviewing:
                case ModalState.ConfirmChanges:
                case ModalState.InsufficientCredits:
                    // A changed basket invalidates the review it was based on.
                    ShowLines();
                    break;
            }
        }

        private void OnIncrementClicked(ShopCartLineView lineView)
        {
            if (lineView.BoundLineId != null)
                cart.Increment(lineView.BoundLineId);
        }

        private void OnDecrementClicked(ShopCartLineView lineView)
        {
            if (lineView.BoundLineId != null)
                cart.Decrement(lineView.BoundLineId);
        }

        private void OnRemoveClicked(ShopCartLineView lineView)
        {
            if (lineView.BoundLineId == null || !cart.TryGet(lineView.BoundLineId, out ShopCartLine? line))
                return;

            LineRemoved?.Invoke(line!);
            cart.Remove(lineView.BoundLineId);
        }

        private void OnFavoriteClicked(ShopCartLineView lineView)
        {
            if (lineView.BoundLineId != null && cart.TryGet(lineView.BoundLineId, out ShopCartLine? line))
                inputData.Favorites.Toggle(line!.Listing, line.Source);
        }

        private void OnFavoritesChanged()
        {
            foreach (ShopCartLineView lineView in activeLines)
            {
                if (lineView.BoundLineId != null && cart.TryGet(lineView.BoundLineId, out ShopCartLine? line))
                    RefreshFavorite(lineView, line!);
            }
        }

        private void RefreshFavorite(ShopCartLineView lineView, ShopCartLine line) =>
            lineView.SetFavorite(inputData.Favorites.CanBeFavorite(line.Listing), inputData.Favorites.IsFavorite(line.Listing));

        private void OnCheckoutClicked()
        {
            if (currentState != ModalState.Lines || cart.Count == 0 || lifeCts == null || lifeCts.IsCancellationRequested)
                return;

            StartCheckoutAsync(lifeCts.Token).Forget();
        }

        private void OnConfirmChangesClicked()
        {
            if (currentState != ModalState.ConfirmChanges || review == null || lifeCts == null || lifeCts.IsCancellationRequested)
                return;

            ProceedAsync(review, lifeCts.Token).Forget();
        }

        private async UniTask StartCheckoutAsync(CancellationToken ct)
        {
            SetUiState(ModalState.Reviewing);

            // The cart can be edited while the review is running, so it works on a copy and is dropped if that happened.
            CartReviewResult reviewResult = await checkoutService.ReviewAsync(new List<ShopCartLine>(cart.Lines), ct);

            if (ct.IsCancellationRequested || currentState != ModalState.Reviewing)
                return;

            if (!reviewResult.Success)
            {
                ShowFailure(ErrorCopy(reviewResult.Error), allowRetry: reviewResult.Error != CreditsPurchaseError.FeatureDisabled);
                return;
            }

            CartReview reviewed = reviewResult.Review!;
            review = reviewed;

            CheckoutStarted?.Invoke(cart.TotalUnits, reviewed.LiveTotalCredits, balanceCredits >= reviewed.LiveTotalCredits);

            if (reviewed.Buyable.Count == 0)
            {
                ShowFailure("None of the items in your cart can be bought right now.", allowRetry: false);
                return;
            }

            if (reviewed.OrderChanged)
            {
                ShowConfirmChanges(reviewed);
                return;
            }

            await ProceedAsync(reviewed, ct);
        }

        private async UniTask ProceedAsync(CartReview reviewed, CancellationToken ct)
        {
            if (DateTime.UtcNow - reviewed.ReviewedAtUtc > REVIEW_TTL)
            {
                await StartCheckoutAsync(ct);
                return;
            }

            if (balanceCredits >= 0 && balanceCredits < reviewed.LiveTotalCredits)
            {
                ShowInsufficientCredits(reviewed.LiveTotalCredits - balanceCredits);
                BuyCreditsPrompted?.Invoke(reviewed.LiveTotalCredits, balanceCredits, reviewed.LiveTotalCredits - balanceCredits);
                return;
            }

            SetUiState(ModalState.Processing);
            NativeWindowManager.RequestTemporaryWindowMode();
            awaitingOwnCheckout = true;

            try
            {
                CartCheckoutResult result = await checkoutService.CheckoutAsync(reviewed, ct);
                ReportResult(result);

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
                awaitingOwnCheckout = false;
                NativeWindowManager.ReleaseTemporaryWindowMode();
            }
        }

        // Renders a checkout this modal session did not start: one that was still running when the modal was reopened.
        private void OnCheckoutCompleted(CartCheckoutResult result)
        {
            if (currentState != ModalState.Processing || awaitingOwnCheckout)
                return;

            checkoutService.AcknowledgeResult();
            ShowResult(result);
        }

        private void ReportResult(CartCheckoutResult result)
        {
            switch (result.Outcome)
            {
                case CartCheckoutOutcome.Completed:
                case CartCheckoutOutcome.PartiallyCompleted:
                    CheckoutCompleted?.Invoke(result);
                    break;
                case CartCheckoutOutcome.InsufficientCredits:
                    long needed = review?.LiveTotalCredits ?? cart.TotalCredits;
                    BuyCreditsPrompted?.Invoke(needed, balanceCredits, ShortfallOf(result, needed));
                    break;
                case CartCheckoutOutcome.Cancelled:
                    CheckoutCancelled?.Invoke(result, MapAnalyticsStep(result.FailedAtStage));
                    break;
                default:
                    CheckoutFailed?.Invoke(result, MapAnalyticsStep(result.FailedAtStage), CreditPurchaseModalController.MapAnalyticsErrorCode(result.FirstError));
                    break;
            }
        }

        private long ShortfallOf(CartCheckoutResult result, long needed) =>
            result.MissingCredits >= 0 ? result.MissingCredits : Math.Max(1L, needed - Math.Max(0, balanceCredits));

        private void ShowResult(CartCheckoutResult result)
        {
            switch (result.Outcome)
            {
                case CartCheckoutOutcome.Completed:
                    SetUiState(ModalState.Success);

                    if (viewInstance != null)
                        viewInstance.SuccessSummaryText.text = string.Format(SUCCESS_SUMMARY_FORMAT, FormatItemCount(result.BoughtUnits.Count));

                    RefreshBalance();
                    break;
                case CartCheckoutOutcome.PartiallyCompleted:
                    SetUiState(ModalState.PartialFailure);

                    if (viewInstance != null)
                    {
                        int total = result.BoughtUnits.Count + result.UnboughtUnits.Count;
                        string reason = result.HasPendingSettlement ? PENDING_SETTLEMENT_COPY : ErrorCopy(result.FirstError);
                        viewInstance.PartialSummaryText.text = string.Format(PARTIAL_SUMMARY_FORMAT, result.BoughtUnits.Count, total, reason);
                    }

                    RefreshBalance();
                    break;
                case CartCheckoutOutcome.InsufficientCredits:
                    ShowInsufficientCredits(ShortfallOf(result, review?.LiveTotalCredits ?? cart.TotalCredits));
                    break;
                case CartCheckoutOutcome.Cancelled:
                    ShowLines();
                    break;
                default:
                    ShowFailure(result.HasPendingSettlement ? PENDING_SETTLEMENT_COPY : ErrorCopy(result.FirstError), allowRetry: !result.HasPendingSettlement);
                    break;
            }
        }

        private void ShowConfirmChanges(CartReview reviewed)
        {
            SetUiState(ModalState.ConfirmChanges);

            if (viewInstance == null)
                return;

            string dropped = reviewed.Dropped.Count > 0
                ? string.Format(CHANGES_DROPPED_FORMAT, reviewed.Dropped.Count, reviewed.Dropped.Count == 1 ? "is" : "are", reviewed.Dropped.Count == 1 ? "was" : "were")
                : string.Empty;

            viewInstance.ChangesSummaryText.text = dropped + string.Format(CHANGES_TOTAL_FORMAT, reviewed.LiveTotalCredits);
            viewInstance.TotalCreditsText.text = reviewed.LiveTotalCredits.ToString();
            viewInstance.ConfirmChangesButton.interactable = reviewed.Buyable.Count > 0;
            UpdateSignatureBadge(reviewed.GroupCount);
        }

        private void ShowInsufficientCredits(long shortfall)
        {
            SetUiState(ModalState.InsufficientCredits);

            if (viewInstance != null)
                viewInstance.ShortfallText.text = string.Format(SHORTFALL_FORMAT, shortfall);
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
                CartCheckoutStage.Signing => many ? string.Format(CONFIRMING_FORMAT, progress.GroupIndex, progress.GroupCount) : CONFIRMING_SINGLE,
                CartCheckoutStage.WaitingSettlement => many ? string.Format(SETTLING_FORMAT, progress.GroupIndex, progress.GroupCount) : SETTLING_SINGLE,
                _ => viewInstance.ProgressStatusText.text,
            };

            viewInstance.CancelConfirmationButton.gameObject.SetActive(progress.Stage == CartCheckoutStage.Signing);
            UpdateSignatureBadge(progress.GroupCount);
        }

        private void UpdateSignatureBadge(int groupCount)
        {
            if (viewInstance == null)
                return;

            viewInstance.SignatureCountBadge.SetActive(groupCount > 1);

            if (groupCount > 1)
                viewInstance.SignatureCountText.text = string.Format(CONFIRMATIONS_FORMAT, groupCount);
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

        private static async UniTask NavigateAfterCloseAsync(Func<CancellationToken, UniTask> navigate, CancellationToken ct)
        {
            try { await navigate(ct); }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                ReportHub.LogException(e, new ReportData(ReportCategory.CREDITS_PURCHASE));
            }
        }

        private async UniTask LoadBalanceAsync(CancellationToken ct)
        {
            IWeb3Identity? identity = identityCache.Identity;

            if (identity == null)
                return;

            try
            {
                UserCreditsResponse credits = await creditsApiClient.GetUserCreditsAsync(identity.Address, ct);

                if (ct.IsCancellationRequested)
                    return;

                balanceCredits = credits.usd.credits;
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                ReportHub.LogWarning(ReportCategory.CREDITS_PURCHASE, $"Cart balance load failed: {e.Message}");
            }
        }

        private void RefreshBalance()
        {
            if (lifeCts != null)
                LoadBalanceAsync(lifeCts.Token).Forget();
        }

        private void SetUiState(ModalState newState)
        {
            currentState = newState;

            if (viewInstance == null)
                return;

            viewInstance.LinesStateContainer.SetActive(newState is ModalState.Lines or ModalState.Reviewing);
            viewInstance.LinesContainer.gameObject.SetActive(newState != ModalState.Reviewing);
            viewInstance.ReviewingSpinner.SetActive(newState == ModalState.Reviewing);
            viewInstance.ConfirmChangesContainer.SetActive(newState == ModalState.ConfirmChanges);
            viewInstance.InsufficientCreditsContainer.SetActive(newState == ModalState.InsufficientCredits);
            viewInstance.ProgressStateContainer.SetActive(newState == ModalState.Processing);
            viewInstance.SuccessStateContainer.SetActive(newState == ModalState.Success);
            viewInstance.PartialFailureStateContainer.SetActive(newState == ModalState.PartialFailure);
            viewInstance.FailedStateContainer.SetActive(newState == ModalState.Failed);
            viewInstance.BuyCreditsButton.gameObject.SetActive(newState is ModalState.ConfirmChanges or ModalState.InsufficientCredits);
            viewInstance.CheckoutButton.interactable = newState == ModalState.Lines && cart.Count > 0;

            if (newState != ModalState.Processing)
                viewInstance.CancelConfirmationButton.gameObject.SetActive(false);

            if (newState is not (ModalState.ConfirmChanges or ModalState.Processing))
                viewInstance.SignatureCountBadge.SetActive(false);
        }

        private void RequestClose() =>
            viewInstance?.CloseButton.onClick.Invoke();

        private static string FormatItemCount(int count) =>
            count == 1 ? ITEM_COUNT_SINGULAR : string.Format(ITEM_COUNT_FORMAT, count);

        private static string MapAnalyticsStep(CartCheckoutStage stage) =>
            stage switch
            {
                CartCheckoutStage.Reviewing => ANALYTICS_STEP_REVIEW,
                CartCheckoutStage.Reserving => "reserving",
                CartCheckoutStage.Signing => "signing",
                CartCheckoutStage.WaitingSettlement => "waiting_settlement",
                _ => ANALYTICS_STEP_CHECKOUT,
            };

        // Web2 copy: no wallet, signature, chain or transaction wording.
        private static string ErrorCopy(CreditsPurchaseError error) =>
            error switch
            {
                CreditsPurchaseError.InsufficientCredits => "You don't have enough credits for this purchase.",
                CreditsPurchaseError.SettlementPending => PENDING_SETTLEMENT_COPY,
                CreditsPurchaseError.SignatureRejected or CreditsPurchaseError.Cancelled => "You cancelled the request.",
                CreditsPurchaseError.PriceChanged => "The price of an item changed. Please review your cart and try again.",
                CreditsPurchaseError.PriceUnavailable => "A price is unavailable right now. Please try again later.",
                CreditsPurchaseError.ListingNotAvailable => "Some items are no longer available for purchase with credits.",
                CreditsPurchaseError.OwnListing => "You cannot buy your own listing.",
                CreditsPurchaseError.TransactionReverted => "The purchase failed. Your credits were not spent.",
                CreditsPurchaseError.RelayerUnavailable => "The purchase service is temporarily unavailable. Please try again later.",
                CreditsPurchaseError.FeatureDisabled => "Buying with credits is not available right now.",
                _ => "The purchase failed. Your credits were not spent.",
            };
    }
}
