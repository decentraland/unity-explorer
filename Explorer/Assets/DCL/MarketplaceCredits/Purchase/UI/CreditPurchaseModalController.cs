using Arch.Core;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Backpack;
using DCL.Browser;
using DCL.CharacterPreview;
using DCL.Diagnostics;
using DCL.Profiles;
using DCL.Profiles.Self;
using DCL.UI.Profiles.Helpers;
using DCL.Web3.Identities;
using MVC;
using Plugins.NativeWindowManager;
using Runtime.Wearables;
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

        private const string INSUFFICIENT_FUNDS_TEXT = "Your Credits and MANA are not enough for this item. Buy Credits or add MANA to continue.";
        private const string INSUFFICIENT_CREDITS_TEXT = "Your Credits are not enough for this item. Buy Credits to continue.";
        private const string MANA_ALONE_REFUSED_TEXT = "This item is priced too low to be paid with MANA alone. Buy Credits to continue.";
        private const float NORMAL_HEIGHT = 911;
        private const float PURCHASING_HEIGHT = 371;
        private const float INSUFFICIENT_FUNDS_HEIGHT = 942;
        private const float FAILED_HEIGHT = 813;
        private const float COMPLETED_HEIGHT = 571;

        // What the popup loses in height when the MANA row is not shown: the row and the gap above it.
        private const float MANA_ROW_HEIGHT = 64;

        private const string ANALYTICS_STEP_QUOTE = "quote";
        private const string ANALYTICS_STEP_BALANCE = "balance";
        private const string ANALYTICS_ERROR_UNKNOWN = "unknown";
        private const string ANALYTICS_DETAIL_NO_IDENTITY = "no_identity";
        private const string ANALYTICS_DETAIL_BALANCE_LOAD_FAILED = "balance_load_failed";
        private const string ANALYTICS_DETAIL_UNHANDLED_EXCEPTION = "unhandled_exception";
        private const int UNKNOWN_MISSING_CREDITS = -1;
        private const string EMOTE_PLAY_LABEL = "Play";
        private const string EMOTE_STOP_LABEL = "Stop";

        private readonly ICreditsPurchaseService purchaseService;
        private readonly IWeb3IdentityCache identityCache;
        private readonly UnityAppWebBrowser webBrowser;
        private readonly ICharacterPreviewFactory characterPreviewFactory;
        private readonly CharacterPreviewEventBus characterPreviewEventBus;
        private readonly SelfProfile selfProfile;
        private readonly ProfileRepositoryWrapper profileRepositoryWrapper;
        private readonly World world;
        private readonly IWearableStorage wearableStorage;
        private readonly NftTypeIconSO rarityInfoPanelBackgrounds;
        private readonly NFTColorsSO rarityColors;
        private readonly NftTypeIconSO rarityBackgrounds;
        private readonly NftTypeIconSO categoryIcons;
        private readonly Func<CancellationToken, UniTask> openGetCreditsPanelAsync;
        private readonly Func<CancellationToken, UniTask> openBackpackAsync;
        private readonly bool isManaEnabled;
        private readonly float hiddenRowsHeight;
        private readonly CancellationTokenSource disposalCts = new ();

        private ModalState currentState;
        private bool settlementPending;
        private CancellationTokenSource? lifeCts;
        private CreditsPurchaseQuote? quote;
        private bool purchaseSucceeded;
        private bool purchaseFailed;
        private bool navigatedAway;
        private float purchaseStartedAt;
        private CreditsPurchaseState lastPurchaseState;
        private URN shortenedItemUrn;
        private CreditPurchaseTryOnCharacterPreviewController? tryOnPreviewController;
        private CancellationTokenSource? characterPreviewCts;
        private bool characterPreviewShown;
        private bool emotePlayingShown;

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Popup;

        public event Action<ShopListingDto, string>? ModalOpened;
        public event Action<ShopListingDto, int>? BuyCreditsPrompted;
        public event Action<ShopListingDto, CreditsPurchaseQuote, PaymentMethod>? PurchaseStarted;
        public event Action<ShopListingDto, CreditsPurchaseQuote, PaymentMethod, string, float>? PurchaseCompleted;
        public event Action<ShopListingDto, string, string, string>? PurchaseFailed;
        public event Action<ShopListingDto, string>? PurchaseCancelled;
        public event Action<ShopListingDto, string, string>? NavigationClicked;
        public event Action<ShopListingDto>? RetryClicked;

        public CreditPurchaseModalController(
            ViewFactoryMethod viewFactory,
            ICreditsPurchaseService purchaseService,
            IWeb3IdentityCache identityCache,
            UnityAppWebBrowser webBrowser,
            ICharacterPreviewFactory characterPreviewFactory,
            CharacterPreviewEventBus characterPreviewEventBus,
            SelfProfile selfProfile,
            ProfileRepositoryWrapper profileRepositoryWrapper,
            World world,
            IWearableStorage wearableStorage,
            NftTypeIconSO rarityInfoPanelBackgrounds,
            NFTColorsSO rarityColors,
            NftTypeIconSO rarityBackgrounds,
            NftTypeIconSO categoryIcons,
            Func<CancellationToken, UniTask> openGetCreditsPanelAsync,
            Func<CancellationToken, UniTask> openBackpackAsync,
            bool isManaEnabled)
            : base(viewFactory)
        {
            this.purchaseService = purchaseService;
            this.identityCache = identityCache;
            this.webBrowser = webBrowser;
            this.characterPreviewFactory = characterPreviewFactory;
            this.characterPreviewEventBus = characterPreviewEventBus;
            this.selfProfile = selfProfile;
            this.profileRepositoryWrapper = profileRepositoryWrapper;
            this.world = world;
            this.wearableStorage = wearableStorage;
            this.rarityInfoPanelBackgrounds = rarityInfoPanelBackgrounds;
            this.rarityColors = rarityColors;
            this.rarityBackgrounds = rarityBackgrounds;
            this.categoryIcons = categoryIcons;
            this.openGetCreditsPanelAsync = openGetCreditsPanelAsync;
            this.openBackpackAsync = openBackpackAsync;
            this.isManaEnabled = isManaEnabled;
            hiddenRowsHeight = isManaEnabled ? 0f : MANA_ROW_HEIGHT;
            purchaseService.StateChanged += OnPurchaseStateChanged;
        }

        public override void Dispose()
        {
            purchaseService.StateChanged -= OnPurchaseStateChanged;
            lifeCts?.SafeCancelAndDispose();
            characterPreviewCts?.SafeCancelAndDispose();
            tryOnPreviewController?.Dispose();
            disposalCts.SafeCancelAndDispose();
        }

        protected override void OnViewInstantiated()
        {
            tryOnPreviewController = new CreditPurchaseTryOnCharacterPreviewController(
                viewInstance!.TryOnCharacterPreviewView,
                characterPreviewFactory,
                world,
                characterPreviewEventBus);
        }

        protected override void OnBeforeViewShow()
        {
            if (viewInstance == null)
                return;

            // The show animation scales to what fits the screen, so it needs the height of the state it opens in.
            viewInstance.ContainerTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, NORMAL_HEIGHT - hiddenRowsHeight);
            viewInstance.TryOnCharacterPreviewView.gameObject.SetActive(IsTryOnAvailable());
            tryOnPreviewController?.OnBeforeShow();
        }

        protected override void OnViewShow()
        {
            lifeCts = new CancellationTokenSource();
            settlementPending = false;
            quote = null;
            purchaseSucceeded = false;
            purchaseFailed = false;
            navigatedAway = false;
            lastPurchaseState = CreditsPurchaseState.ResolvingListing;
            shortenedItemUrn = new URN(inputData.ItemUrn).Shorten();

            if (viewInstance != null)
            {
                string rarity = inputData.Listing.rarity;
                Color rarityColor = rarityColors.GetColor(rarity);
                Sprite? rarityBackground = rarityBackgrounds.GetTypeImage(rarity);
                Sprite? categoryIcon = categoryIcons.GetTypeImage(inputData.CategoryIconKey);

                viewInstance.ItemName.text = inputData.Listing.name;
                viewInstance.CreatorName.text = string.Empty;
                viewInstance.RarityLabel.text = rarity;
                viewInstance.RarityLabel.color = rarityColor;
                viewInstance.RarityBackground.color = new Color(rarityColor.r, rarityColor.g, rarityColor.b, viewInstance.RarityBackground.color.a);
                viewInstance.RarityBackgroundFull.sprite = rarityInfoPanelBackgrounds.GetTypeImage(rarity);

                if (inputData.ItemThumbnail != null)
                {
                    viewInstance.ItemThumbnail.sprite = inputData.ItemThumbnail;
                    viewInstance.ItemThumbnailCompleted.sprite = inputData.ItemThumbnail;
                }

                if (rarityBackground != null)
                {
                    viewInstance.ItemBackground.sprite = rarityBackground;
                    viewInstance.ItemBackgroundCompleted.sprite = rarityBackground;
                }

                if (categoryIcon != null)
                {
                    viewInstance.ItemCategory.sprite = categoryIcon;
                    viewInstance.ItemCategoryCompleted.sprite = categoryIcon;
                }

                viewInstance.ItemCategoryBackground.color = rarityColor;
                viewInstance.ItemCategoryBackgroundCompleted.color = rarityColor;

                viewInstance.ConfirmButton.onClick.AddListener(OnConfirmClicked);
                viewInstance.RetryButton.onClick.AddListener(OnRetryClicked);
                viewInstance.GetCreditsButton.onClick.AddListener(OnGetCreditsClicked);
                viewInstance.OpenMarketplaceButton.onClick.AddListener(OnOpenMarketplaceClicked);
                viewInstance.ToBackpackButton.onClick.AddListener(OnToBackpackClicked);
                viewInstance.TryOnEmoteToggleButton.onClick.AddListener(OnEmoteToggleClicked);
            }

            LoadQuoteAndBalanceAsync(lifeCts.Token).Forget();
            LoadCreatorNameAsync(lifeCts.Token).Forget();

            ModalOpened?.Invoke(inputData.Listing, inputData.Source);
        }

        protected override void OnViewClose()
        {
            ResetCharacterPreview();

            if (viewInstance != null)
            {
                viewInstance.ConfirmButton.onClick.RemoveListener(OnConfirmClicked);
                viewInstance.RetryButton.onClick.RemoveListener(OnRetryClicked);
                viewInstance.GetCreditsButton.onClick.RemoveListener(OnGetCreditsClicked);
                viewInstance.OpenMarketplaceButton.onClick.RemoveListener(OnOpenMarketplaceClicked);
                viewInstance.ToBackpackButton.onClick.RemoveListener(OnToBackpackClicked);
                viewInstance.TryOnEmoteToggleButton.onClick.RemoveListener(OnEmoteToggleClicked);
            }

            if (!purchaseSucceeded && !navigatedAway)
                PurchaseCancelled?.Invoke(inputData.Listing, MapAnalyticsStageName(currentState));

            lifeCts.SafeCancelAndDispose();

            inputData.Closed?.Invoke(ResolveOutcome(purchaseSucceeded, purchaseFailed, settlementPending));
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
                viewInstance.PaymentSelector.Clear(isManaEnabled);
            }

            if (identityCache.Identity == null)
            {
                ReportPurchaseFailed(ANALYTICS_STEP_QUOTE, ANALYTICS_ERROR_UNKNOWN, ANALYTICS_DETAIL_NO_IDENTITY);
                ShowFailure("You need to be signed in to buy items.", allowRetry: false);
                return;
            }

            // The whole row, not its tradeId: a mint has none, and everything the quote needs is already here.
            CreditsQuoteResult quoteResult = await purchaseService.QuoteAsync(inputData.Listing, ct);

            if (ct.IsCancellationRequested)
                return;

            if (!quoteResult.Success)
            {
                ReportHub.LogWarning(ReportCategory.CREDITS_PURCHASE,
                    $"Quote failed for {inputData.Listing.contractAddress}-{inputData.Listing.itemId}: {quoteResult.Error} {quoteResult.Message}");
                ReportPurchaseFailed(ANALYTICS_STEP_QUOTE, MapAnalyticsErrorCode(quoteResult.Error), MapAnalyticsErrorDetail(quoteResult.Error));
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
                PaymentOptions options = await purchaseService.GetPaymentOptionsAsync(resolved, ct);

                if (ct.IsCancellationRequested)
                    return;

                bool canPay = options.Options.Count > 0;

                // Nothing payable and no credits balance means the balance read failed, not that the buyer is short.
                if (!canPay && options.CreditsBalanceCents == null)
                {
                    ReportPurchaseFailed(ANALYTICS_STEP_BALANCE, ANALYTICS_ERROR_UNKNOWN, ANALYTICS_DETAIL_BALANCE_LOAD_FAILED);
                    ShowFailure("Could not load your balance.", allowRetry: true);
                    return;
                }

                ShowBalances(options);

                if (viewInstance != null)
                {
                    viewInstance.CannotAffortText.text = InsufficientCopy(options);
                    viewInstance.PaymentSelector.Show(options, isManaEnabled);
                }

                SetUiState(canPay ? ModalState.ReadyToConfirm : ModalState.InsufficientCredits);

                if (!canPay)
                    BuyCreditsPrompted?.Invoke(inputData.Listing, MissingCredits(resolved, options));
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                ReportHub.LogException(e, new ReportData(ReportCategory.CREDITS_PURCHASE));
                ReportPurchaseFailed(ANALYTICS_STEP_BALANCE, ANALYTICS_ERROR_UNKNOWN, ANALYTICS_DETAIL_BALANCE_LOAD_FAILED);
                ShowFailure("Could not load your balance.", allowRetry: true);
            }
        }

        private string InsufficientCopy(PaymentOptions options)
        {
            if (!isManaEnabled)
                return INSUFFICIENT_CREDITS_TEXT;

            return options.IsManaAloneRefused ? MANA_ALONE_REFUSED_TEXT : INSUFFICIENT_FUNDS_TEXT;
        }

        private static int MissingCredits(in CreditsPurchaseQuote quote, PaymentOptions options) =>
            options.CreditsBalanceCents is { } balanceCents
                ? (int)Math.Max(0L, quote.Credits - (balanceCents / PaymentOptionsCalculator.CENTS_PER_CREDIT))
                : UNKNOWN_MISSING_CREDITS;

        private void ShowBalances(PaymentOptions options)
        {
            if (viewInstance == null)
                return;

            viewInstance.BalanceCreditsText.text = options.CreditsBalanceCents is { } balanceCents
                ? PaymentAmountFormat.Credits(balanceCents / PaymentOptionsCalculator.CENTS_PER_CREDIT)
                : PaymentAmountFormat.UNKNOWN;

            viewInstance.BalanceManaText.text = options.ManaBalanceWei is { } balanceWei
                ? PaymentAmountFormat.ManaBalance(balanceWei)
                : PaymentAmountFormat.UNKNOWN;
        }

        private async UniTask LoadCreatorNameAsync(CancellationToken ct)
        {
            string creator = inputData.Listing.creator;

            try
            {
                Profile.CompactInfo? profile = await profileRepositoryWrapper.GetProfileAsync(creator, ct);

                if (ct.IsCancellationRequested || viewInstance == null)
                    return;

                viewInstance.CreatorName.text = profile is { ValidatedName: { Length: > 0 } } compact
                    ? compact.ValidatedName
                    : ShortenWallet(creator);
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                ReportHub.LogWarning(ReportCategory.CREDITS_PURCHASE, $"Creator profile lookup failed for {creator}: {e.Message}");

                if (!ct.IsCancellationRequested && viewInstance != null)
                    viewInstance.CreatorName.text = ShortenWallet(creator);
            }
        }

        private static string ShortenWallet(string wallet) =>
            wallet.Length > 10 ? string.Concat(wallet[..6], "...", wallet[^4..]) : wallet;

        private void OnConfirmClicked()
        {
            if (currentState != ModalState.ReadyToConfirm || quote == null || viewInstance == null || lifeCts == null || lifeCts.IsCancellationRequested)
                return;

            if (!viewInstance.PaymentSelector.Selection.TryGetSelected(out PaymentOption option))
                return;

            purchaseStartedAt = UnityEngine.Time.realtimeSinceStartup;
            lastPurchaseState = CreditsPurchaseState.ResolvingListing;
            PurchaseStarted?.Invoke(inputData.Listing, quote.Value, option.Method);
            PurchaseAsync(quote.Value, option, lifeCts.Token).Forget();
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

        private void ShowCharacterPreview()
        {
            if (characterPreviewShown)
                return;

            characterPreviewShown = true;
            viewInstance?.TryOnEmoteToggleButton.gameObject.SetActive(false);

            characterPreviewCts = characterPreviewCts.SafeRestart();
            LoadCharacterPreviewAsync(characterPreviewCts.Token).Forget();
        }

        private async UniTask LoadCharacterPreviewAsync(CancellationToken ct)
        {
            CreditPurchaseTryOnCharacterPreviewController? previewController = tryOnPreviewController;

            if (previewController == null)
                return;

            try
            {

                ProfileReadResult profileResult = await selfProfile.ProfileAsync(ct);

                if (ct.IsCancellationRequested)
                    return;

                if (!profileResult.IsOk(out Profile? profile))
                {
                    ReportHub.LogWarning(ReportCategory.CREDITS_PURCHASE, "Try-on preview aborted: own profile is unavailable.");
                    ResetCharacterPreview();
                    return;
                }

                if (inputData.IsEmote)
                {
                    previewController.TryOnEmote(profile.Avatar, shortenedItemUrn);
                    SetEmoteToggleLabel(playing: true);
                    viewInstance?.TryOnEmoteToggleButton.gameObject.SetActive(true);
                    TrackEmotePlaybackAsync(previewController, ct).Forget();
                }
                else
                    previewController.TryOnWearable(profile.Avatar, shortenedItemUrn, inputData.Listing.wearableCategory, wearableStorage);
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                ReportHub.LogException(e, new ReportData(ReportCategory.CREDITS_PURCHASE));
                ResetCharacterPreview();
            }
        }

        private void OnEmoteToggleClicked()
        {
            if (!characterPreviewShown || !inputData.IsEmote || tryOnPreviewController == null)
                return;

            if (tryOnPreviewController.IsPlayingEmote())
            {
                tryOnPreviewController.StopEmotes();
                SetEmoteToggleLabel(playing: false);
            }
            else
            {
                tryOnPreviewController.PlayTryOnEmote(shortenedItemUrn);
                SetEmoteToggleLabel(playing: true);
            }
        }

        // Mirrors the preview emote state onto the toggle label so a non-looping emote that ends on its
        // own flips back to "Play". Only transitions update the label, so the optimistic value set on click
        // survives the frames between the play intent and the animator entering the emote state.
        private async UniTask TrackEmotePlaybackAsync(CreditPurchaseTryOnCharacterPreviewController previewController, CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                bool playing = previewController.IsPlayingEmote();

                if (playing != emotePlayingShown)
                    SetEmoteToggleLabel(playing);

                await UniTask.Yield(PlayerLoopTiming.PostLateUpdate, ct).SuppressCancellationThrow();
            }
        }

        private void SetEmoteToggleLabel(bool playing)
        {
            emotePlayingShown = playing;

            if (viewInstance != null)
                viewInstance.TryOnEmoteToggleButtonText.text = playing ? EMOTE_STOP_LABEL : EMOTE_PLAY_LABEL;
        }

        private void ResetCharacterPreview()
        {
            if (!characterPreviewShown)
                return;

            characterPreviewCts?.SafeCancelAndDispose();
            characterPreviewCts = null;
            characterPreviewShown = false;
            emotePlayingShown = false;
            tryOnPreviewController?.OnHide();
            viewInstance?.TryOnEmoteToggleButton.gameObject.SetActive(false);
        }

        private bool IsTryOnAvailable() =>
            inputData.IsEmote || inputData.Listing.wearableCategory != WearableCategories.Categories.BODY_SHAPE;

        private async UniTask PurchaseAsync(CreditsPurchaseQuote confirmedQuote, PaymentOption option, CancellationToken ct)
        {
            SetUiState(ModalState.Purchasing);
            NativeWindowManager.RequestTemporaryWindowMode();

            try
            {
                CreditsPurchaseResult result = await purchaseService.PurchaseAsync(confirmedQuote, option, ct);

                // A Cancelled result after the modal closed would reopen the try-on preview on the hidden view.
                if (ct.IsCancellationRequested)
                    return;

                if (result.Success)
                {
                    purchaseSucceeded = true;
                    PurchaseCompleted?.Invoke(inputData.Listing, confirmedQuote, option.Method, result.TxHash ?? string.Empty, UnityEngine.Time.realtimeSinceStartup - purchaseStartedAt);
                    SetUiState(ModalState.Success);
                    RefreshBalanceAsync(confirmedQuote, ct).Forget();
                }
                else
                {
                    // Cancelled is the modal-close path, reported once as PurchaseCancelled, not as a failure.
                    if (result.Error != CreditsPurchaseError.Cancelled)
                        ReportPurchaseFailed(MapAnalyticsStepName(lastPurchaseState), MapAnalyticsErrorCode(result.Error), MapAnalyticsErrorDetail(result.Error));

                    ShowError(result.Error);
                }
            }
            catch (Exception e)
            {
                ReportHub.LogException(e, new ReportData(ReportCategory.CREDITS_PURCHASE));
                ReportPurchaseFailed(MapAnalyticsStepName(lastPurchaseState), ANALYTICS_ERROR_UNKNOWN, ANALYTICS_DETAIL_UNHANDLED_EXCEPTION);
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
                    BuyCreditsPrompted?.Invoke(inputData.Listing, UNKNOWN_MISSING_CREDITS);
                    ShowFailure("You don't have enough Credits for this purchase.", allowRetry: true);
                    break;
                case CreditsPurchaseError.InsufficientMana:
                    ShowFailure("You don't have enough MANA for this purchase.", allowRetry: true);
                    break;
                case CreditsPurchaseError.ManaApprovalFailed:
                    ShowFailure("MANA could not be authorized for this purchase. Nothing was spent.", allowRetry: true);
                    break;
                case CreditsPurchaseError.SettlementPending:
                    settlementPending = true;
                    ShowFailure("Your purchase is still processing. What it costs is reserved and it will complete automatically — check back soon.", allowRetry: false);
                    break;
                case CreditsPurchaseError.SignatureRejected:
                    ShowFailure("The signature request was rejected.", allowRetry: true);
                    break;
                case CreditsPurchaseError.SignatureTimedOut:
                    ShowFailure("The confirmation timed out. Nothing was spent.", allowRetry: true);
                    break;
                case CreditsPurchaseError.PriceChanged:
                    ShowFailure("The price of this item changed. Please reopen the item to see the new price.", allowRetry: false);
                    break;
                case CreditsPurchaseError.PriceUnavailable:
                    ShowFailure("The price of this item is unavailable right now. Please try again later or open it in the marketplace.", allowRetry: true);
                    break;
                case CreditsPurchaseError.ListingNotAvailable:
                    ShowFailure("This item is no longer available for purchase.", allowRetry: false);
                    break;
                case CreditsPurchaseError.OwnListing:
                    ShowFailure("You cannot buy your own listing.", allowRetry: false);
                    break;
                case CreditsPurchaseError.TransactionReverted:
                    ShowFailure("The purchase failed on-chain. Nothing was spent.", allowRetry: true);
                    break;
                case CreditsPurchaseError.PurchaseInProgress:
                    ShowFailure("Another purchase is still processing. Wait for it to finish and try again.", allowRetry: true);
                    break;
                case CreditsPurchaseError.RelayerUnavailable:
                    ShowFailure("The purchase service is temporarily unavailable. Please try again later.", allowRetry: true);
                    break;
                default:
                    ShowFailure("The purchase failed. Nothing was spent.", allowRetry: true);
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
                CreditsPurchaseState.ApprovingMana => "Waiting for you to authorize MANA...",
                CreditsPurchaseState.Signing => "Waiting for your signature...",
                CreditsPurchaseState.WaitingSettlement => "Completing the purchase...",
                _ => viewInstance.ProgressStatusText.text,
            };
        }

        private async UniTask RefreshBalanceAsync(CreditsPurchaseQuote boughtQuote, CancellationToken ct)
        {
            try
            {
                PaymentOptions options = await purchaseService.GetPaymentOptionsAsync(boughtQuote, ct);

                // A failed read keeps the balances the popup already shows.
                if (!ct.IsCancellationRequested && options.CreditsBalanceCents != null)
                    ShowBalances(options);
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

        private void ReportPurchaseFailed(string step, string errorCode, string errorDetail)
        {
            purchaseFailed = true;
            PurchaseFailed?.Invoke(inputData.Listing, step, errorCode, errorDetail);
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

            float height = newState switch
                           {
                               ModalState.Success => COMPLETED_HEIGHT,
                               ModalState.Purchasing => PURCHASING_HEIGHT,
                               ModalState.InsufficientCredits => INSUFFICIENT_FUNDS_HEIGHT - hiddenRowsHeight,
                               ModalState.Failed => FAILED_HEIGHT,
                               _ => NORMAL_HEIGHT - hiddenRowsHeight,
                           };

            viewInstance.ContainerTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
            viewInstance.FitToScreen();
            viewInstance.ConfirmStateContainer.SetActive(newState is ModalState.LoadingBalance or ModalState.ReadyToConfirm);
            viewInstance.ProgressStateContainer.SetActive(purchasing);
            viewInstance.SuccessStateContainer.SetActive(newState == ModalState.Success);
            viewInstance.FailedStateContainer.SetActive(newState == ModalState.Failed);
            viewInstance.InsufficientCreditsContainer.SetActive(newState == ModalState.InsufficientCredits);
            viewInstance.BalanceLoadingSpinner.SetActive(newState == ModalState.LoadingBalance);
            viewInstance.BalanceCreditsText.gameObject.SetActive(newState != ModalState.LoadingBalance);
            viewInstance.BalanceManaGroup.SetActive(isManaEnabled && newState != ModalState.LoadingBalance);
            viewInstance.Item.SetActive(newState is ModalState.LoadingBalance or ModalState.ReadyToConfirm or ModalState.InsufficientCredits);

            viewInstance.ConfirmButton.interactable = newState == ModalState.ReadyToConfirm;

            bool previewVisible = newState is ModalState.LoadingBalance or ModalState.ReadyToConfirm or ModalState.InsufficientCredits && IsTryOnAvailable();

            if (previewVisible)
                ShowCharacterPreview();
            else
                ResetCharacterPreview();
        }

        private void RequestClose() =>
            viewInstance?.CloseButton.onClick.Invoke();

        /// <summary>
        ///     How a closed modal ended. A completed purchase wins over an earlier failed attempt, and a failed
        ///     attempt that was never followed by a completed one stays a failure even if the user then closes.
        ///     A purchase that is still settling is neither: it is paid for and not yet delivered, so it ends
        ///     as dismissed whatever happened in earlier attempts.
        /// </summary>
        public static CreditPurchaseModalOutcome ResolveOutcome(bool purchaseSucceeded, bool purchaseFailed, bool settlementPending)
        {
            if (purchaseSucceeded)
                return CreditPurchaseModalOutcome.Purchased;

            if (settlementPending)
                return CreditPurchaseModalOutcome.Dismissed;

            return purchaseFailed ? CreditPurchaseModalOutcome.Failed : CreditPurchaseModalOutcome.Dismissed;
        }

        /// <summary>
        ///     Coarse analytics buckets, a superset of the web shop's
        ///     (user_rejected | insufficient_credits | not_for_sale | unknown).
        /// </summary>
        public static string MapAnalyticsErrorCode(CreditsPurchaseError error) =>
            error switch
            {
                CreditsPurchaseError.SignatureRejected => "user_rejected",
                CreditsPurchaseError.Cancelled => "user_rejected",
                CreditsPurchaseError.SignatureTimedOut => "signature_timeout",
                CreditsPurchaseError.InsufficientCredits => "insufficient_credits",
                CreditsPurchaseError.InsufficientMana => "insufficient_mana",
                CreditsPurchaseError.ListingNotAvailable => "not_for_sale",
                CreditsPurchaseError.OwnListing => "not_for_sale",
                CreditsPurchaseError.FeatureDisabled => "not_for_sale",
                CreditsPurchaseError.PriceChanged => "price_error",
                CreditsPurchaseError.PriceUnavailable => "price_error",
                CreditsPurchaseError.SettlementPending => "settlement_pending",
                CreditsPurchaseError.TransactionReverted => "transaction_failed",
                CreditsPurchaseError.ManaApprovalFailed => "transaction_failed",
                CreditsPurchaseError.RelayerUnavailable => "service_unavailable",
                CreditsPurchaseError.AuthorizationFailed => "service_unavailable",
                CreditsPurchaseError.PurchaseInProgress => "purchase_in_progress",
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
                CreditsPurchaseError.SignatureTimedOut => "signature_timed_out",
                CreditsPurchaseError.RelayerUnavailable => "relayer_unavailable",
                CreditsPurchaseError.TransactionReverted => "transaction_reverted",
                CreditsPurchaseError.SettlementPending => "settlement_pending",
                CreditsPurchaseError.Cancelled => "cancelled",
                CreditsPurchaseError.EncodingFailed => "encoding_failed",
                CreditsPurchaseError.InsufficientMana => "insufficient_mana",
                CreditsPurchaseError.ManaApprovalFailed => "mana_approval_failed",
                CreditsPurchaseError.PurchaseInProgress => "purchase_in_progress",
                _ => "unknown_error",
            };

        /// <summary>The purchase step a failure is attributed to, from the last progress state.</summary>
        public static string MapAnalyticsStepName(CreditsPurchaseState state) =>
            state switch
            {
                CreditsPurchaseState.ResolvingListing => "resolving_listing",
                CreditsPurchaseState.Authorizing => "authorizing",
                CreditsPurchaseState.ApprovingMana => "approving_mana",
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
