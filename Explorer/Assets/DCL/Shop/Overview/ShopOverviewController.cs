using Cysharp.Threading.Tasks;
using DCL.Communities;
using DCL.Diagnostics;
using DCL.MarketplaceCredits.Purchase;
using DCL.MarketplaceCredits.Purchase.Cart;
using DCL.MarketplaceCredits.Purchase.UI;
using DCL.NotificationsBus;
using DCL.NotificationsBus.NotificationTypes;
using DCL.Utilities.Extensions;
using DCL.Utility.Types;
using System;
using System.Collections.Generic;
using System.Threading;
using Utility;

namespace DCL.Shop
{
    public class ShopOverviewController : IDisposable
    {
        private const string LOAD_ERROR_MESSAGE = "There was an error loading the shop. Please try again.";
        private const string OUTFIT_ADD_ERROR_MESSAGE = "Couldn't add the outfit to your cart. Please try again.";
        private const string OUTFIT_NONE_LEFT_MESSAGE = "None of this outfit's items are available anymore.";
        private const string OUTFIT_ADDED_MESSAGE = "Outfit added to your cart.";
        private const string OUTFIT_PARTIALLY_ADDED_FORMAT = "{0} of {1} items added to your cart.";

        private readonly ShopOverviewView view;
        private readonly ShopCatalogService catalog;
        private readonly ShopItemCardPresenter cards;
        private readonly ShopCreatorNameCache creatorNames;
        private readonly ThumbnailLoader thumbnailLoader;
        private readonly MarketplaceShopAPIClient api;
        private readonly ShopCart cart;
        private readonly HashSet<string> outfitsBeingAdded = new ();
        private readonly List<string> creatorsScratch = new ();
        private readonly HashSet<string> creatorsSeen = new ();

        private CancellationTokenSource? loadCts;
        private CancellationTokenSource? actionsCts;
        private bool isActive;
        private bool trendingEmpty;
        private bool newCreationsEmpty;

        public event Action<ShopOutfitAddResult>? OutfitAddedToCart;
        public event Action? ViewAllClicked;

        public ShopOverviewController(
            ShopOverviewView view,
            ShopCatalogService catalog,
            ShopItemCardPresenter cards,
            ShopCreatorNameCache creatorNames,
            ThumbnailLoader thumbnailLoader,
            MarketplaceShopAPIClient api,
            ShopCart cart)
        {
            this.view = view;
            this.catalog = catalog;
            this.cards = cards;
            this.creatorNames = creatorNames;
            this.thumbnailLoader = thumbnailLoader;
            this.api = api;
            this.cart = cart;

            view.ItemAddToCartClicked += OnItemAddToCartClicked;
            view.ItemBuyClicked += OnItemBuyClicked;
            view.ItemViewClicked += OnItemViewClicked;
            view.OutfitAddClicked += OnOutfitAddClicked;
            view.ViewAllClicked += OnViewAllClicked;
        }

        public void Dispose()
        {
            view.ItemAddToCartClicked -= OnItemAddToCartClicked;
            view.ItemBuyClicked -= OnItemBuyClicked;
            view.ItemViewClicked -= OnItemViewClicked;
            view.OutfitAddClicked -= OnOutfitAddClicked;
            view.ViewAllClicked -= OnViewAllClicked;
            loadCts.SafeCancelAndDispose();
            actionsCts.SafeCancelAndDispose();
        }

        public void Activate()
        {
            isActive = true;
            view.gameObject.SetActive(true);
            view.ResetScroll();
            Reload();
        }

        public void Deactivate()
        {
            isActive = false;
            loadCts.SafeCancelAndDispose();
            actionsCts.SafeCancelAndDispose();
            outfitsBeingAdded.Clear();
            view.ReleaseAllCards();
            view.gameObject.SetActive(false);
        }

        public void Reload()
        {
            if (!isActive)
                return;

            loadCts = loadCts.SafeRestart();
            actionsCts = actionsCts.SafeRestart();
            view.SetEmptyVisible(false);
            LoadAllAsync(loadCts.Token).Forget();
        }

        public void RefreshCartState()
        {
            foreach (ShopItemCardView card in view.ActiveItemCards)
                cards.RefreshCartState(card);

            foreach (ShopOutfitCardView card in view.ActiveOutfitCards)
            {
                if (card.Model != null)
                    card.SetCtaEnabled(HasPurchasableItems(card.Model));
            }
        }

        public void RefreshCountdowns(long nowUnixSeconds)
        {
            foreach (ShopItemCardView card in view.ActiveItemCards)
                card.RefreshSaleCountdown(nowUnixSeconds);
        }

        private async UniTask LoadAllAsync(CancellationToken ct)
        {
            await UniTask.WhenAll(
                LoadRowAsync(view.TrendingCarousel, catalog.GetTrendingAsync, isTrending: true, ct),
                LoadRowAsync(view.NewCreationsCarousel, catalog.GetNewCreationsAsync, isTrending: false, ct),
                LoadOutfitsAsync(ct));

            if (ct.IsCancellationRequested)
                return;

            view.SetEmptyVisible(trendingEmpty && newCreationsEmpty);
            ResolveCreatorNamesAsync(ct).Forget();
        }

        private async UniTask LoadRowAsync(ShopCarouselView row, Func<CancellationToken, UniTask<IReadOnlyList<ShopItemCardModel>>> fetch, bool isTrending, CancellationToken ct)
        {
            row.gameObject.SetActive(true);
            row.SetLoading(true);

            Result<IReadOnlyList<ShopItemCardModel>> result = await fetch(ct).SuppressToResultAsync(ReportCategory.UI);

            if (ct.IsCancellationRequested)
                return;

            view.ReleaseRowCards(row);

            if (!result.Success)
            {
                NotificationsBusController.Instance.AddNotification(new ServerErrorNotification(LOAD_ERROR_MESSAGE));
                row.gameObject.SetActive(false);
                SetRowEmpty(isTrending, true);
                return;
            }

            IReadOnlyList<ShopItemCardModel> models = result.Value;
            long now = NowUnixSeconds();

            foreach (ShopItemCardModel model in models)
                cards.Bind(view.RentItemCard(row), model, now);

            row.SetItemCount(models.Count);
            row.SetLoading(false);
            row.gameObject.SetActive(models.Count > 0);
            SetRowEmpty(isTrending, models.Count == 0);
        }

        private async UniTask LoadOutfitsAsync(CancellationToken ct)
        {
            ShopCarouselView row = view.OutfitsRow;
            row.gameObject.SetActive(true);
            row.SetLoading(true);

            Result<ShopOutfitsDataset> result = await catalog.GetOutfitsAsync(ct).SuppressToResultAsync(ReportCategory.UI);

            if (ct.IsCancellationRequested)
                return;

            view.ReleaseOutfitCards();

            if (!result.Success)
            {
                row.gameObject.SetActive(false);
                return;
            }

            ShopOutfitsDataset dataset = result.Value;

            foreach (ShopOutfitModel model in dataset.Outfits)
            {
                ShopOutfitCardView card = view.RentOutfitCard();
                card.Bind(model, thumbnailLoader, dataset.ResolutionFailed);
                card.SetCtaEnabled(!dataset.ResolutionFailed && HasPurchasableItems(model));
            }

            row.SetItemCount(dataset.Outfits.Count);
            row.SetLoading(false);
            row.gameObject.SetActive(dataset.Outfits.Count > 0);
        }

        private void SetRowEmpty(bool isTrending, bool empty)
        {
            if (isTrending)
                trendingEmpty = empty;
            else
                newCreationsEmpty = empty;
        }

        private async UniTaskVoid ResolveCreatorNamesAsync(CancellationToken ct)
        {
            creatorsScratch.Clear();
            creatorsSeen.Clear();

            foreach (ShopItemCardView card in view.ActiveItemCards)
            {
                if (card.Model != null && creatorsSeen.Add(card.Model.Creator))
                    creatorsScratch.Add(card.Model.Creator);
            }

            if (creatorsScratch.Count == 0)
                return;

            bool changed = await creatorNames.ResolveAsync(creatorsScratch, ct);

            if (ct.IsCancellationRequested || !changed)
                return;

            foreach (ShopItemCardView card in view.ActiveItemCards)
            {
                if (card.Model != null)
                    card.SetCreatorName(creatorNames.GetDisplayName(card.Model.Creator));
            }
        }

        private bool HasPurchasableItems(ShopOutfitModel model)
        {
            foreach (ShopItemCardModel item in model.ResolvedItems)
            {
                if (!item.IsNotForSale && !cards.IsOwnListing(item) && !cards.IsInCart(item))
                    return true;
            }

            return false;
        }

        private void OnItemAddToCartClicked(ShopItemCardView card)
        {
            if (actionsCts == null)
                return;

            ShopCartSource source = IsTrendingCard(card) ? ShopCartSource.Trending : ShopCartSource.NewCreations;
            cards.AddToCartAsync(card, source, actionsCts.Token).Forget();
        }

        private void OnItemBuyClicked(ShopItemCardView card)
        {
            if (actionsCts == null)
                return;

            string source = IsTrendingCard(card) ? CreditPurchaseModalControllerParams.SOURCE_SHOP_TRENDING : CreditPurchaseModalControllerParams.SOURCE_SHOP_NEW_CREATIONS;
            cards.BuyAsync(card, source, actionsCts.Token).Forget();
        }

        private void OnItemViewClicked(ShopItemCardView card)
        {
            if (card.Model != null)
                cards.View(card.Model);
        }

        private void OnOutfitAddClicked(ShopOutfitCardView card)
        {
            if (actionsCts != null)
                AddOutfitAsync(card, actionsCts.Token).Forget();
        }

        private void OnViewAllClicked() =>
            ViewAllClicked?.Invoke();

        private bool IsTrendingCard(ShopItemCardView card) =>
            card.transform.parent == view.TrendingCarousel.Track;

        private async UniTaskVoid AddOutfitAsync(ShopOutfitCardView card, CancellationToken ct)
        {
            ShopOutfitModel? model = card.Model;

            if (model == null || !outfitsBeingAdded.Add(model.Id))
                return;

            var purchasable = new List<ShopItemCardModel>(model.ResolvedItems.Count);
            var skippedUnavailable = model.MissingCount;
            var skippedInCart = 0;
            var skippedOwn = 0;

            foreach (ShopItemCardModel item in model.ResolvedItems)
            {
                if (item.IsNotForSale)
                    skippedUnavailable++;
                else if (cards.IsOwnListing(item))
                    skippedOwn++;
                else if (cards.IsInCart(item))
                    skippedInCart++;
                else
                    purchasable.Add(item);
            }

            if (purchasable.Count == 0)
            {
                outfitsBeingAdded.Remove(model.Id);
                NotificationsBusController.Instance.AddNotification(new ServerErrorNotification(OUTFIT_NONE_LEFT_MESSAGE));
                return;
            }

            card.SetAdding(true);

            var lookups = new UniTask<Result<ShopListingDto?>>[purchasable.Count];

            for (var i = 0; i < purchasable.Count; i++)
                lookups[i] = api.GetShopListingForItemAsync(purchasable[i].ContractAddress, purchasable[i].ItemId ?? string.Empty, false, ct).SuppressToResultAsync(ReportCategory.UI);

            Result<ShopListingDto?>[] results = await UniTask.WhenAll(lookups);

            outfitsBeingAdded.Remove(model.Id);

            if (ct.IsCancellationRequested)
                return;

            card.SetAdding(false);

            foreach (Result<ShopListingDto?> result in results)
            {
                if (result.Success)
                    continue;

                NotificationsBusController.Instance.AddNotification(new ServerErrorNotification(OUTFIT_ADD_ERROR_MESSAGE));
                return;
            }

            var added = 0;

            foreach (Result<ShopListingDto?> result in results)
            {
                ShopListingDto? listing = result.Value;

                if (listing == null || listing.priceCredits <= 0 || (listing.IsPrimary() && listing.available <= 0))
                    continue;

                if (cart.Add(listing, ShopCartSource.Outfit, model.Id))
                    added++;
            }

            skippedUnavailable += purchasable.Count - added;
            int total = model.ResolvedItems.Count + model.MissingCount;

            if (added == 0)
                NotificationsBusController.Instance.AddNotification(new ServerErrorNotification(OUTFIT_NONE_LEFT_MESSAGE));
            else if (added == total)
                NotificationsBusController.Instance.AddNotification(new DefaultSuccessNotification(OUTFIT_ADDED_MESSAGE));
            else
                NotificationsBusController.Instance.AddNotification(new DefaultSuccessNotification(string.Format(OUTFIT_PARTIALLY_ADDED_FORMAT, added, total)));

            OutfitAddedToCart?.Invoke(new ShopOutfitAddResult(model.Id, added, skippedUnavailable, skippedInCart, skippedOwn, model.TotalCredits));
        }

        private static long NowUnixSeconds() =>
            DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }
}
