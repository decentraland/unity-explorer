using Cysharp.Threading.Tasks;
using DCL.Backpack.Gifting.Styling;
using DCL.Browser;
using DCL.Communities;
using DCL.Diagnostics;
using DCL.FeatureFlags;
using DCL.Input;
using DCL.Input.Component;
using DCL.MarketplaceCredits.Purchase;
using DCL.MarketplaceCredits.Purchase.Cart;
using DCL.MarketplaceCredits.Purchase.Cart.UI;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Passport.Modules;
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

        private readonly ShopView view;
        private readonly RectTransform rectTransform;
        private readonly ICursor cursor;
        private readonly IInputBlock inputBlock;
        private readonly IMVCManager mvcManager;
        private readonly ICreditsPurchaseService purchaseService;
        private readonly ICreditsCartCheckoutService cartCheckoutService;
        private readonly ShopCatalogService catalog;
        private readonly ShopItemCardPresenter presenter;
        private readonly Func<CancellationToken, UniTask> openBuyCreditsAsync;

        private CancellationTokenSource? countdownCts;
        private CancellationTokenSource? modalCts;
        private bool isSectionActivated;
        private bool isPageShown;

        public ShopOverviewController OverviewController { get; }

        public ShopCollectiblesController CollectiblesController { get; }

        public ShopCart Cart { get; }

        public ShopPage CurrentPage { get; private set; }

        public event Action<ShopPage>? PageViewed;

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
            ThumbnailLoader thumbnailLoader,
            ProfileRepositoryWrapper profileRepositoryWrapper,
            UnityAppWebBrowser webBrowser,
            IDecentralandUrlsSource urlsSource,
            IWeb3IdentityCache identityCache,
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
            var creatorNames = new ShopCreatorNameCache(profileRepositoryWrapper);
            presenter = new ShopItemCardPresenter(cart, buyHandler, api, webBrowser, urlsSource, identityCache, styling, thumbnailLoader, creatorNames);
            OverviewController = new ShopOverviewController(view.OverviewView, catalog, presenter, creatorNames, thumbnailLoader, api, cart);
            CollectiblesController = new ShopCollectiblesController(view.CollectiblesView, view.FiltersView, api, presenter, creatorNames, styling);

            view.PageTabClicked += OpenPage;
            view.CartButtonClicked += OnCartButtonClicked;
            view.BuyCreditsClicked += OnBuyCreditsClicked;
            view.CollectiblesView.SearchBarSelected += DisableShortcutsInput;
            view.CollectiblesView.SearchBarDeselected += RestoreShortcutsInput;
            OverviewController.ViewAllClicked += OnViewAllClicked;
            CollectiblesController.BackToOverviewClicked += OnBackToOverviewClicked;

            cart.Changed += OnCartChanged;
            purchaseService.StateChanged += OnPurchaseStateChanged;
            cartCheckoutService.CheckoutCompleted += OnCartCheckoutCompleted;
        }

        public void Dispose()
        {
            view.PageTabClicked -= OpenPage;
            view.CartButtonClicked -= OnCartButtonClicked;
            view.BuyCreditsClicked -= OnBuyCreditsClicked;
            view.CollectiblesView.SearchBarSelected -= DisableShortcutsInput;
            view.CollectiblesView.SearchBarDeselected -= RestoreShortcutsInput;
            OverviewController.ViewAllClicked -= OnViewAllClicked;
            CollectiblesController.BackToOverviewClicked -= OnBackToOverviewClicked;

            Cart.Changed -= OnCartChanged;
            purchaseService.StateChanged -= OnPurchaseStateChanged;
            cartCheckoutService.CheckoutCompleted -= OnCartCheckoutCompleted;

            OverviewController.Dispose();
            CollectiblesController.Dispose();
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
            view.SetBuyCreditsVisible(FeaturesRegistry.Instance.IsEnabled(FeatureId.UserCredits) && userAllowed);
            view.SetCartBadge(Cart.TotalUnits);

            CollectiblesController.ResetFilters();
            isPageShown = false;
            OpenPage(ShopPage.Overview);
            cursor.Unlock();

            countdownCts = countdownCts.SafeRestart();
            TickCountdownsAsync(countdownCts.Token).Forget();
        }

        public void Deactivate()
        {
            if (view.IsSearchBarFocused)
                RestoreShortcutsInput();

            isSectionActivated = false;
            isPageShown = false;
            OverviewController.Deactivate();
            CollectiblesController.Deactivate();
            countdownCts.SafeCancelAndDispose();
            view.SetViewActive(false);
        }

        public void Animate(int triggerId) =>
            view.PlayAnimator(triggerId);

        public void ResetAnimator() =>
            view.ResetAnimator();

        public RectTransform GetRectTransform() =>
            rectTransform;

        public void OpenPage(ShopPage page)
        {
            if (isPageShown && page == CurrentPage)
                return;

            if (isPageShown)
            {
                if (CurrentPage == ShopPage.Overview)
                    OverviewController.Deactivate();
                else
                    CollectiblesController.Deactivate();
            }

            CurrentPage = page;
            isPageShown = true;
            view.ShowPage(page);

            if (page == ShopPage.Overview)
                OverviewController.Activate();
            else
                CollectiblesController.Activate();

            PageViewed?.Invoke(page);
        }

        private void OnViewAllClicked() =>
            OpenPage(ShopPage.Collectibles);

        private void OnBackToOverviewClicked() =>
            OpenPage(ShopPage.Overview);

        private void OnCartChanged()
        {
            view.SetCartBadge(Cart.TotalUnits);

            if (!isSectionActivated)
                return;

            if (CurrentPage == ShopPage.Overview)
                OverviewController.RefreshCartState();
            else
                CollectiblesController.RefreshCartState();
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

            if (CurrentPage == ShopPage.Overview)
                OverviewController.Reload();
            else
                CollectiblesController.Reload();
        }

        private void OnCartButtonClicked()
        {
            modalCts = modalCts.SafeRestart();
            ShowCartAsync(modalCts.Token).Forget();
        }

        private void OnBuyCreditsClicked()
        {
            modalCts = modalCts.SafeRestart();
            OpenBuyCreditsAsync(modalCts.Token).Forget();
        }

        private async UniTaskVoid ShowCartAsync(CancellationToken ct)
        {
            try { await mvcManager.ShowAsync(ShopCartModalController.IssueCommand(new ShopCartModalParams(ShopCartModalParams.SOURCE_SHOP_HEADER)), ct); }
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

                if (CurrentPage == ShopPage.Overview)
                    OverviewController.RefreshCountdowns(now);
                else
                    CollectiblesController.RefreshCountdowns(now);
            }
        }

        private void DisableShortcutsInput() =>
            inputBlock.Disable(InputMapComponent.Kind.Shortcuts, InputMapComponent.Kind.InWorldCamera);

        private void RestoreShortcutsInput() =>
            inputBlock.Enable(InputMapComponent.Kind.Shortcuts, InputMapComponent.Kind.InWorldCamera);
    }
}
