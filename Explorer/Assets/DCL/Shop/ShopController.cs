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
using DCL.MarketplaceCredits.Purchase.Names;
using DCL.MarketplaceCredits.Purchase.UI;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.NotificationsBus;
using DCL.NotificationsBus.NotificationTypes;
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
        private const string FAVORITE_ERROR_MESSAGE = "Your favorites could not be updated. Please try again.";

        // Hidden until those sections ship; the tabs stay in the prefab so they only need removing from this list
        private static readonly ShopSection[] UNRELEASED_SECTIONS = { ShopSection.Lands };

        private readonly ShopView view;
        private readonly Vector3[] cornersCache = new Vector3[4];
        private readonly RectTransform rectTransform;
        private readonly ICursor cursor;
        private readonly IInputBlock inputBlock;
        private readonly IMVCManager mvcManager;
        private readonly ICreditsPurchaseService purchaseService;
        private readonly CreditsCartCheckoutService cartCheckoutService;
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

        public ShopFavoritesSectionController FavoritesController { get; }

        public ShopNamesSectionController NamesController { get; }

        public ShopFavorites Favorites { get; }

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
            CreditsCartCheckoutService cartCheckoutService,
            NameClaimService nameClaimService,
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
            Favorites = new ShopFavorites(api, identityCache);
            presenter = new ShopItemCardPresenter(cart, buyHandler, api, webBrowser, urlsSource, identityCache, styling, thumbnailLoader, creators);
            OverviewController = new ShopOverviewController(view.OverviewView, catalog, presenter, creators, thumbnailLoader, api, cart);
            WearablesController = new ShopGridSectionController(view.WearablesView, api, presenter, creators, ShopCategoryTree.WEARABLE);
            EmotesController = new ShopGridSectionController(view.EmotesView, api, presenter, creators, ShopCategoryTree.EMOTE);
            FavoritesController = new ShopFavoritesSectionController(view.FavoritesView, api, presenter, creators, Favorites);
            NamesController = new ShopNamesSectionController(view.NamesView, view.PreviewNametagRoot, view.PreviewNametag, nameClaimService, api, selfProfile, mvcManager, webBrowser, urlsSource);
            Preview = new ShopCharacterPreviewController(view.CharacterPreview, characterPreviewFactory, world, characterPreviewEventBus, wearableStorage, selfProfile);
            ItemPanel = new ShopItemPanelController(view.ItemPanel, presenter, creators, api, styling, rarityInfoPanelBackgrounds, thumbnailLoader, wearablesProvider, emoteProvider, Favorites);

            view.SectionTabClicked += ShowSection;
            view.CartButtonClicked += OnCartButtonClicked;
            view.SearchBarSelected += DisableShortcutsInput;
            view.SearchBarDeselected += RestoreShortcutsInput;
            view.SearchChanged += OnSearchChanged;
            view.SortChanged += OnSortChanged;
            view.NamesView.InputSelected += DisableShortcutsInput;
            NamesController.TabChanged += OnNamesTabChanged;
            view.NamesView.InputDeselected += RestoreShortcutsInput;
            OverviewController.ViewAllClicked += OnBrowseClicked;
            OverviewController.ItemClicked += OpenItem;
            OverviewController.OpenCartRequested += OnOpenCartRequested;
            WearablesController.BrowseClicked += OnBrowseClicked;
            WearablesController.ItemClicked += OnGridItemClicked;
            EmotesController.BrowseClicked += OnBrowseClicked;
            EmotesController.ItemClicked += OnGridItemClicked;
            FavoritesController.BrowseClicked += OnBrowseClicked;
            FavoritesController.ItemClicked += OnGridItemClicked;
            Favorites.ToggleFailed += OnFavoriteToggleFailed;
            ItemPanel.CloseClicked += CloseItem;
            ItemPanel.OpenCartRequested += OnOpenCartRequested;
            ItemPanel.WearableResolved += Preview.TryOnWearable;
            ItemPanel.PreviewUnavailable += Preview.RestoreOwnAvatar;

            cart.Changed += OnCartChanged;
            cart.ItemAdded += OnCartItemAdded;
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
            view.NamesView.InputSelected -= DisableShortcutsInput;
            NamesController.TabChanged -= OnNamesTabChanged;
            view.NamesView.InputDeselected -= RestoreShortcutsInput;
            OverviewController.ViewAllClicked -= OnBrowseClicked;
            OverviewController.ItemClicked -= OpenItem;
            WearablesController.BrowseClicked -= OnBrowseClicked;
            WearablesController.ItemClicked -= OnGridItemClicked;
            EmotesController.BrowseClicked -= OnBrowseClicked;
            EmotesController.ItemClicked -= OnGridItemClicked;
            FavoritesController.BrowseClicked -= OnBrowseClicked;
            FavoritesController.ItemClicked -= OnGridItemClicked;
            Favorites.ToggleFailed -= OnFavoriteToggleFailed;
            ItemPanel.CloseClicked -= CloseItem;
            ItemPanel.OpenCartRequested -= OnOpenCartRequested;
            ItemPanel.WearableResolved -= Preview.TryOnWearable;
            ItemPanel.PreviewUnavailable -= Preview.RestoreOwnAvatar;

            Cart.Changed -= OnCartChanged;
            Cart.ItemAdded -= OnCartItemAdded;
            purchaseService.StateChanged -= OnPurchaseStateChanged;
            cartCheckoutService.CheckoutCompleted -= OnCartCheckoutCompleted;

            OverviewController.Dispose();
            WearablesController.Dispose();
            EmotesController.Dispose();
            FavoritesController.Dispose();
            NamesController.Dispose();
            ItemPanel.Dispose();
            Favorites.Dispose();
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
            Favorites.Reload();
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
            FavoritesController.Deactivate();
            DeactivateNames();
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
                SectionFor(CurrentSection)?.Deactivate();
                DeactivateNames();
            }

            CurrentSection = section;
            isSectionShown = true;
            view.ShowSection(section);

            RefreshSearchAndSort();

            IShopGridSection? grid = SectionFor(section);

            if (grid != null)
                grid.Activate();
            else if (section == ShopSection.All)
                OverviewController.Activate();
            else if (section == ShopSection.Names)
                NamesController.Activate();

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
            FavoritesController.SetSelected(model.Key);

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

        private IShopGridSection? SectionFor(ShopSection section) =>
            section switch
            {
                ShopSection.Wearables => WearablesController,
                ShopSection.Emotes => EmotesController,
                ShopSection.Favorites => FavoritesController,
                _ => null,
            };

        private void RefreshSearchAndSort()
        {
            ShopGridSectionController? filteredGrid = FilteredGridFor(CurrentSection);
            bool resaleNames = CurrentSection == ShopSection.Names && NamesController.IsResaleShown;

            view.SetSearchAndSortVisible(filteredGrid != null || resaleNames);
            view.LimitSortOptions(resaleNames ? ShopNamesResaleController.SORT_OPTION_COUNT : null);

            if (filteredGrid != null)
            {
                view.SetSearchText(filteredGrid.Filters.SearchText);
                view.SetSort(filteredGrid.Filters.Sort);
            }
            else if (resaleNames)
            {
                view.SetSearchText(NamesController.Resale.SearchText);
                view.SetSort(NamesController.Resale.Sort);
            }
        }

        // A focused field that is hidden never reports its deselection, which would leave the shortcuts blocked
        private void OnNamesTabChanged(ShopNamesTab _)
        {
            if (view.IsSearchBarFocused)
                RestoreShortcutsInput();

            RefreshSearchAndSort();
        }

        // Search and sort apply to the catalog grids and to the resale names grid
        private ShopGridSectionController? FilteredGridFor(ShopSection section) =>
            section switch
            {
                ShopSection.Wearables => WearablesController,
                ShopSection.Emotes => EmotesController,
                _ => null,
            };

        // A focused field that is hidden never reports its deselection, which would leave the shortcuts blocked
        private void DeactivateNames()
        {
            if (view.NamesView.IsInputFocused)
                RestoreShortcutsInput();

            NamesController.Deactivate();
        }

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
            FavoritesController.SetSelected(null);

            if (restoreAvatar)
                Preview.RestoreOwnAvatar();
        }

        private void SetCompact(bool compact)
        {
            OverviewController.SetCompact(compact);
            WearablesController.SetCompact(compact);
            EmotesController.SetCompact(compact);
            FavoritesController.SetCompact(compact);
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

        private void OnFavoriteToggleFailed() =>
            NotificationsBusController.Instance.AddNotification(new ServerErrorNotification(FAVORITE_ERROR_MESSAGE));

        private void OnSearchChanged(string text)
        {
            if (CurrentSection == ShopSection.Names)
                NamesController.Resale.SetSearch(text);
            else
                FilteredGridFor(CurrentSection)?.SetSearch(text);
        }

        private void OnSortChanged(ShopSortOption sort)
        {
            if (CurrentSection == ShopSection.Names)
                NamesController.Resale.SetSort(sort);
            else
                FilteredGridFor(CurrentSection)?.SetSort(sort);
        }

        private void OnCartChanged()
        {
            view.SetCartBadge(Cart.TotalUnits);

            if (!isSectionActivated)
                return;

            OverviewController.RefreshCartState();
            SectionFor(CurrentSection)?.RefreshCartState();
            ItemPanel.RefreshCartState();
        }

        private void OnCartItemAdded(ShopCartLine line, ShopCartSource source) =>
            view.PlayCartBump();

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
            SectionFor(CurrentSection)?.Reload();
        }

        private void OnCartButtonClicked() =>
            OpenCart(ShopCartModalParams.SOURCE_SHOP_HEADER);

        private void OnOpenCartRequested() =>
            OpenCart(ShopCartModalParams.SOURCE_ADD_TO_CART);

        private void OpenCart(string source)
        {
            modalCts = modalCts.SafeRestart();
            ShowCartAsync(source, modalCts.Token).Forget();
        }

        private async UniTaskVoid ShowCartAsync(string source, CancellationToken ct)
        {
            ((RectTransform)view.CartButton.transform).GetWorldCorners(cornersCache);
            var parameter = new ShopCartModalParams(source, cornersCache[BOTTOM_RIGHT_CORNER], Favorites);

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
                SectionFor(CurrentSection)?.RefreshCountdowns(now);
            }
        }

        private void DisableShortcutsInput() =>
            inputBlock.Disable(InputMapComponent.Kind.Shortcuts, InputMapComponent.Kind.InWorldCamera);

        private void RestoreShortcutsInput() =>
            inputBlock.Enable(InputMapComponent.Kind.Shortcuts, InputMapComponent.Kind.InWorldCamera);
    }
}
