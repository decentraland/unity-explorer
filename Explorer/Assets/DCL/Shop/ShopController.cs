using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Emotes;
using DCL.AvatarRendering.Wearables;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Backpack;
using DCL.Backpack.Gifting.Styling;
using DCL.Browser;
using DCL.CharacterPreview;
using DCL.Communities;
using DCL.Diagnostics;
using DCL.FeatureFlags;
using DCL.Input;
using DCL.Input.Component;
using DCL.MarketplaceCredits.Purchase;
using DCL.MarketplaceCredits.Purchase.Cart;
using DCL.MarketplaceCredits.Purchase.Cart.UI;
using DCL.MarketplaceCredits.Purchase.UI;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Passport.Modules;
using DCL.Profiles.Self;
using DCL.UI;
using DCL.UI.Profiles.Helpers;
using DCL.Web3.Identities;
using MVC;
using System;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.Shop
{
    public class ShopController : ISection, IDisposable
    {
        private const int COUNTDOWN_TICK_MS = 1000;
        private const int BOTTOM_RIGHT_CORNER = 3;

        // Hidden until those sections ship; the tabs stay in the prefab so they only need removing from this list
        private static readonly ShopSection[] UNRELEASED_SECTIONS = { ShopSection.Names, ShopSection.Lands, ShopSection.Favorites };

        private readonly ShopView view;
        private readonly Vector3[] cornersCache = new Vector3[4];
        private readonly RectTransform rectTransform;
        private readonly ICursor cursor;
        private readonly IInputBlock inputBlock;
        private readonly IMVCManager mvcManager;
        private readonly ICreditsPurchaseService purchaseService;
        private readonly ICreditsCartCheckoutService cartCheckoutService;
        private readonly ShopCatalogService catalog;
        private readonly ShopCreatorCache creators;
        private readonly ShopItemCardPresenter presenter;
        private readonly Func<CancellationToken, UniTask> openBuyCreditsAsync;

        private CancellationTokenSource? countdownCts;
        private CancellationTokenSource? modalCts;
        private bool isSectionActivated;
        private bool isSectionShown;
        private bool isItemPanelOpen;

        public ShopOverviewController OverviewController { get; }

        public ShopGridSectionController WearablesController { get; }

        public ShopGridSectionController EmotesController { get; }

        public ShopCharacterPreviewController Preview { get; }

        public ShopItemPanelController ItemPanel { get; }

        public ShopCart Cart { get; }

        public ShopSection CurrentSection { get; private set; }

        public event Action<ShopSection>? SectionViewed;

        public ShopController(
            ShopView view,
            ICursor cursor,
            IInputBlock inputBlock,
            IMVCManager mvcManager,
            MarketplaceShopAPIClient api,
            ShopCart cart,
            ICreditsPurchaseService purchaseService,
            ICreditsCartCheckoutService cartCheckoutService,
            CreditPurchaseBuyHandler buyHandler,
            IWearableStylingCatalog styling,
            NftTypeIconSO rarityInfoPanelBackgrounds,
            ThumbnailLoader thumbnailLoader,
            ProfileRepositoryWrapper profileRepositoryWrapper,
            UnityAppWebBrowser webBrowser,
            IDecentralandUrlsSource urlsSource,
            IWeb3IdentityCache identityCache,
            ICharacterPreviewFactory characterPreviewFactory,
            World world,
            CharacterPreviewEventBus characterPreviewEventBus,
            IWearableStorage wearableStorage,
            SelfProfile selfProfile,
            IWearablesProvider wearablesProvider,
            IEmoteProvider emoteProvider,
            Func<CancellationToken, UniTask> openBuyCreditsAsync)
        {
            this.view = view;
            rectTransform = view.transform.parent.GetComponent<RectTransform>();
            this.cursor = cursor;
            this.inputBlock = inputBlock;
            this.mvcManager = mvcManager;
            this.purchaseService = purchaseService;
            this.cartCheckoutService = cartCheckoutService;
            this.openBuyCreditsAsync = openBuyCreditsAsync;
            Cart = cart;

            catalog = new ShopCatalogService(api);
            creators = new ShopCreatorCache(profileRepositoryWrapper);
            presenter = new ShopItemCardPresenter(cart, buyHandler, api, webBrowser, urlsSource, identityCache, styling, thumbnailLoader, creators);
            OverviewController = new ShopOverviewController(view.OverviewView, catalog, presenter, creators, thumbnailLoader, api, cart);
            WearablesController = new ShopGridSectionController(view.WearablesView, api, presenter, creators, ShopCategoryTree.WEARABLE);
            EmotesController = new ShopGridSectionController(view.EmotesView, api, presenter, creators, ShopCategoryTree.EMOTE);
            Preview = new ShopCharacterPreviewController(view.CharacterPreview, characterPreviewFactory, world, characterPreviewEventBus, wearableStorage, selfProfile);
            ItemPanel = new ShopItemPanelController(view.ItemPanel, presenter, creators, api, styling, rarityInfoPanelBackgrounds, thumbnailLoader, wearablesProvider, emoteProvider);

            view.SectionTabClicked += ShowSection;
            view.CartButtonClicked += OnCartButtonClicked;
            view.SearchBarSelected += DisableShortcutsInput;
            view.SearchBarDeselected += RestoreShortcutsInput;
            view.SearchChanged += OnSearchChanged;
            view.SortChanged += OnSortChanged;
            OverviewController.ViewAllClicked += OnBrowseClicked;
            OverviewController.ItemClicked += OpenItem;
            WearablesController.BrowseClicked += OnBrowseClicked;
            WearablesController.ItemClicked += OnGridItemClicked;
            EmotesController.BrowseClicked += OnBrowseClicked;
            EmotesController.ItemClicked += OnGridItemClicked;
            ItemPanel.CloseClicked += CloseItem;
            ItemPanel.WearableResolved += Preview.TryOnWearable;
            ItemPanel.WearableUnavailable += Preview.RestoreOwnAvatar;

            cart.Changed += OnCartChanged;
            purchaseService.StateChanged += OnPurchaseStateChanged;
            cartCheckoutService.CheckoutCompleted += OnCartCheckoutCompleted;

            view.SetItemPanelOpen(false);

            foreach (ShopSection section in UNRELEASED_SECTIONS)
                view.SetSectionTabVisible(section, false);
        }

        public void Dispose()
        {
            view.SectionTabClicked -= ShowSection;
            view.CartButtonClicked -= OnCartButtonClicked;
            view.SearchBarSelected -= DisableShortcutsInput;
            view.SearchBarDeselected -= RestoreShortcutsInput;
            view.SearchChanged -= OnSearchChanged;
            view.SortChanged -= OnSortChanged;
            OverviewController.ViewAllClicked -= OnBrowseClicked;
            OverviewController.ItemClicked -= OpenItem;
            WearablesController.BrowseClicked -= OnBrowseClicked;
            WearablesController.ItemClicked -= OnGridItemClicked;
            EmotesController.BrowseClicked -= OnBrowseClicked;
            EmotesController.ItemClicked -= OnGridItemClicked;
            ItemPanel.CloseClicked -= CloseItem;
            ItemPanel.WearableResolved -= Preview.TryOnWearable;
            ItemPanel.WearableUnavailable -= Preview.RestoreOwnAvatar;

            Cart.Changed -= OnCartChanged;
            purchaseService.StateChanged -= OnPurchaseStateChanged;
            cartCheckoutService.CheckoutCompleted -= OnCartCheckoutCompleted;

            OverviewController.Dispose();
            WearablesController.Dispose();
            EmotesController.Dispose();
            ItemPanel.Dispose();
            Preview.Dispose();
            creators.Dispose();
            countdownCts.SafeCancelAndDispose();
            modalCts.SafeCancelAndDispose();
        }

        public void Activate()
        {
            if (isSectionActivated)
                return;

            isSectionActivated = true;

            bool userAllowed = CreditsFeatureAccess.Instance.IsUserAllowed();
            bool purchasesEnabled = FeaturesRegistry.Instance.IsEnabled(FeatureId.CreditsWearablePurchase) && FeaturesRegistry.Instance.IsEnabled(FeatureId.UserCredits) && userAllowed;
            presenter.PurchasesEnabled = purchasesEnabled;

            view.SetViewActive(true);
            view.SetCartButtonVisible(purchasesEnabled);
            view.SetCartBadge(Cart.TotalUnits);

            WearablesController.ResetFilters();
            EmotesController.ResetFilters();
            isSectionShown = false;
            Preview.Show();
            ShowSection(ShopSection.All);
            cursor.Unlock();

            countdownCts = countdownCts.SafeRestart();
            TickCountdownsAsync(countdownCts.Token).Forget();
        }

        public void Deactivate()
        {
            if (view.IsSearchBarFocused)
                RestoreShortcutsInput();

            isSectionActivated = false;
            isSectionShown = false;
            CloseItemPanel(restoreAvatar: false);
            Preview.Hide();
            OverviewController.Deactivate();
            WearablesController.Deactivate();
            EmotesController.Deactivate();
            countdownCts.SafeCancelAndDispose();
            view.SetViewActive(false);
        }

        public void Animate(int triggerId) =>
            view.PlayAnimator(triggerId);

        public void ResetAnimator() =>
            view.ResetAnimator();

        public RectTransform GetRectTransform() =>
            rectTransform;

        public void ShowSection(ShopSection section)
        {
            if (isSectionShown && section == CurrentSection)
                return;

            if (Array.IndexOf(UNRELEASED_SECTIONS, section) >= 0)
                return;

            if (isSectionShown)
            {
                CloseItem();
                OverviewController.Deactivate();
                GridFor(CurrentSection)?.Deactivate();
            }

            CurrentSection = section;
            isSectionShown = true;
            view.ShowSection(section);

            ShopGridSectionController? grid = GridFor(section);
            view.SetSearchAndSortVisible(grid != null);

            if (grid != null)
            {
                view.SetSearchText(grid.Filters.SearchText);
                view.SetSort(grid.Filters.Sort);
                grid.Activate();
            }
            else if (section == ShopSection.All)
                OverviewController.Activate();

            SectionViewed?.Invoke(section);
        }

        /// <summary>
        ///     Opens the right-side panel for the item and dresses the preview with it.
        /// </summary>
        public void OpenItem(ShopItemCardModel model, ShopCartSource source)
        {
            if (!isItemPanelOpen)
            {
                isItemPanelOpen = true;
                view.SetItemPanelOpen(true);
                SetCompact(true);
            }

            bool wasPlayingEmote = ItemPanel.Model?.IsEmote == true;
            ItemPanel.Show(model, source, BuySourceFor(source), Preview.BodyShape);
            OverviewController.SetSelected(model.Key);
            WearablesController.SetSelected(model.Key);
            EmotesController.SetSelected(model.Key);

            // A wearable dresses the avatar once its entity resolves (see WearableResolved); until then an emote must not keep playing
            if (model.IsEmote)
                Preview.TryOnEmote(model.Urn);
            else if (wasPlayingEmote)
                Preview.RestoreOwnAvatar();
        }

        public void CloseItem() =>
            CloseItemPanel(restoreAvatar: true);

        /// <summary>
        ///     Opens the shop's buy-credits flow; the header credits pill points here.
        /// </summary>
        public void OpenBuyCredits()
        {
            modalCts = modalCts.SafeRestart();
            OpenBuyCreditsAsync(modalCts.Token).Forget();
        }

        private ShopGridSectionController? GridFor(ShopSection section) =>
            section switch
            {
                ShopSection.Wearables => WearablesController,
                ShopSection.Emotes => EmotesController,
                _ => null,
            };

        private void CloseItemPanel(bool restoreAvatar)
        {
            if (!isItemPanelOpen)
                return;

            isItemPanelOpen = false;
            ItemPanel.Hide();
            view.SetItemPanelOpen(false);
            SetCompact(false);
            OverviewController.SetSelected(null);
            WearablesController.SetSelected(null);
            EmotesController.SetSelected(null);

            if (restoreAvatar)
                Preview.RestoreOwnAvatar();
        }

        private void SetCompact(bool compact)
        {
            OverviewController.SetCompact(compact);
            WearablesController.SetCompact(compact);
            EmotesController.SetCompact(compact);
        }

        private static string BuySourceFor(ShopCartSource source) =>
            source switch
            {
                ShopCartSource.Trending => CreditPurchaseModalControllerParams.SOURCE_SHOP_TRENDING,
                ShopCartSource.NewCreations => CreditPurchaseModalControllerParams.SOURCE_SHOP_NEW_CREATIONS,
                _ => CreditPurchaseModalControllerParams.SOURCE_SHOP_GRID,
            };

        private void OnGridItemClicked(ShopItemCardModel model) =>
            OpenItem(model, ShopCartSource.Grid);

        private void OnBrowseClicked() =>
            ShowSection(ShopSection.Wearables);

        private void OnSearchChanged(string text) =>
            GridFor(CurrentSection)?.SetSearch(text);

        private void OnSortChanged(ShopSortOption sort) =>
            GridFor(CurrentSection)?.SetSort(sort);

        private void OnCartChanged()
        {
            view.SetCartBadge(Cart.TotalUnits);

            if (!isSectionActivated)
                return;

            OverviewController.RefreshCartState();
            GridFor(CurrentSection)?.RefreshCartState();
            ItemPanel.RefreshCartState();
        }

        private void OnPurchaseStateChanged(CreditsPurchaseState state)
        {
            if (state == CreditsPurchaseState.Success)
                InvalidateAndReload();
        }

        private void OnCartCheckoutCompleted(CartCheckoutResult result)
        {
            if (result.AnyBought)
                InvalidateAndReload();
        }

        private void InvalidateAndReload()
        {
            catalog.Invalidate();

            if (!isSectionActivated)
                return;

            OverviewController.Reload();
            GridFor(CurrentSection)?.Reload();
        }

        private void OnCartButtonClicked()
        {
            modalCts = modalCts.SafeRestart();
            ShowCartAsync(modalCts.Token).Forget();
        }

        private async UniTaskVoid ShowCartAsync(CancellationToken ct)
        {
            ((RectTransform)view.CartButton.transform).GetWorldCorners(cornersCache);
            var parameter = new ShopCartModalParams(ShopCartModalParams.SOURCE_SHOP_HEADER, cornersCache[BOTTOM_RIGHT_CORNER]);

            try { await mvcManager.ShowAsync(ShopCartModalController.IssueCommand(parameter), ct); }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                ReportHub.LogException(e, new ReportData(ReportCategory.CREDITS_PURCHASE));
            }
        }

        private async UniTaskVoid OpenBuyCreditsAsync(CancellationToken ct)
        {
            try { await openBuyCreditsAsync(ct); }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                ReportHub.LogException(e, new ReportData(ReportCategory.CREDITS_PURCHASE));
            }
        }

        private async UniTaskVoid TickCountdownsAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                if (await UniTask.Delay(COUNTDOWN_TICK_MS, cancellationToken: ct).SuppressCancellationThrow())
                    return;

                long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                OverviewController.RefreshCountdowns(now);
                GridFor(CurrentSection)?.RefreshCountdowns(now);
            }
        }

        private void DisableShortcutsInput() =>
            inputBlock.Disable(InputMapComponent.Kind.Shortcuts, InputMapComponent.Kind.InWorldCamera);

        private void RestoreShortcutsInput() =>
            inputBlock.Enable(InputMapComponent.Kind.Shortcuts, InputMapComponent.Kind.InWorldCamera);
    }
}
