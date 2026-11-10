using Cysharp.Threading.Tasks;
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
    /// <summary>
    ///     The grid of the account's favorite items, newest first. The ids come from <see cref="ShopFavorites" /> and
    ///     the cards are read from the catalog one page of ids at a time.
    /// </summary>
    public class ShopFavoritesSectionController : IShopGridSection, IDisposable
    {
        private const int PAGE_SIZE = 48;
        private const string EMPTY_MESSAGE = "You have no favorites yet.";
        private const string LOAD_ERROR_MESSAGE = "There was an error loading your favorites. Please try again.";

        private readonly ShopGridSectionView view;
        private readonly MarketplaceShopAPIClient api;
        private readonly ShopItemCardPresenter cards;
        private readonly ShopCreatorCache creatorNames;
        private readonly ShopFavorites favorites;
        private readonly List<ShopItemCardModel> models = new ();
        private readonly List<string> ids = new ();
        private readonly List<string> pageIds = new (PAGE_SIZE);
        private readonly List<string> creatorsScratch = new ();
        private readonly HashSet<string> creatorsSeen = new ();

        private CancellationTokenSource? lifeCts;
        private CancellationTokenSource? fetchCts;
        private CancellationTokenSource? creatorNamesCts;
        private CancellationTokenSource? actionsCts;
        private bool isActive;
        private bool isLoadingPage;
        private bool anySaleShown;
        private int nextIdIndex;
        private string? selectedKey;

        public event Action? BrowseClicked;
        public event Action<ShopItemCardModel>? ItemClicked;

        public ShopFavoritesSectionController(
            ShopGridSectionView view,
            MarketplaceShopAPIClient api,
            ShopItemCardPresenter cards,
            ShopCreatorCache creatorNames,
            ShopFavorites favorites)
        {
            this.view = view;
            this.api = api;
            this.cards = cards;
            this.creatorNames = creatorNames;
            this.favorites = favorites;

            view.InitializeGrid(ModelAt, BindCard);

            view.GridNearEnd += RequestNextPage;
            view.RetryClicked += Reload;
            view.BrowseClicked += OnBrowseClicked;
            view.CardAddToCartClicked += OnCardAddToCartClicked;
            view.CardBuyClicked += OnCardBuyClicked;
            view.CardClicked += OnCardClicked;
            cards.ListingResolved += OnListingResolved;
            favorites.Loaded += OnFavoritesLoaded;
            favorites.Changed += OnFavoriteChanged;
        }

        public void Dispose()
        {
            view.GridNearEnd -= RequestNextPage;
            view.RetryClicked -= Reload;
            view.BrowseClicked -= OnBrowseClicked;
            view.CardAddToCartClicked -= OnCardAddToCartClicked;
            view.CardBuyClicked -= OnCardBuyClicked;
            view.CardClicked -= OnCardClicked;
            cards.ListingResolved -= OnListingResolved;
            favorites.Loaded -= OnFavoritesLoaded;
            favorites.Changed -= OnFavoriteChanged;

            fetchCts.SafeCancelAndDispose();
            creatorNamesCts.SafeCancelAndDispose();
            actionsCts.SafeCancelAndDispose();
            lifeCts.SafeCancelAndDispose();
        }

        public void Activate()
        {
            isActive = true;
            view.gameObject.SetActive(true);
            lifeCts = lifeCts.SafeRestart();
            actionsCts = actionsCts.SafeRestartLinked(lifeCts.Token);
            Reload();
        }

        public void Deactivate()
        {
            isActive = false;
            fetchCts.SafeCancelAndDispose();
            creatorNamesCts.SafeCancelAndDispose();
            actionsCts.SafeCancelAndDispose();
            lifeCts.SafeCancelAndDispose();
            isLoadingPage = false;
            view.SetLoadingPage(false);
            view.SetLoadingMore(false);
            view.gameObject.SetActive(false);
        }

        public void Reload()
        {
            if (!isActive)
                return;

            fetchCts.SafeCancelAndDispose();
            isLoadingPage = true;
            view.SetLoadingPage(true);
            favorites.Reload();
        }

        public void SetCompact(bool compact) =>
            view.SetColumns(compact ? ShopGridSectionView.COMPACT_COLUMNS : ShopGridSectionView.DEFAULT_COLUMNS);

        public void SetSelected(string? key)
        {
            selectedKey = key;
            view.ForEachShownCard(card => card.SetSelected(card.Model != null && card.Model.Key == selectedKey));
        }

        public void RefreshCartState() =>
            view.ForEachShownCard(cards.RefreshCartState);

        public void RefreshCountdowns(long nowUnixSeconds)
        {
            if (anySaleShown)
                view.ForEachShownCard(card => card.RefreshSaleCountdown(nowUnixSeconds));
        }

        private ShopItemCardModel? ModelAt(int index) =>
            index >= 0 && index < models.Count ? models[index] : null;

        private void BindCard(ShopItemCardView card, ShopItemCardModel model)
        {
            cards.Bind(card, model, NowUnixSeconds());
            card.SetSelected(model.Key == selectedKey);
        }

        private void OnFavoritesLoaded(bool success)
        {
            if (!isActive || lifeCts == null)
                return;

            if (!success)
            {
                OnPageFailed(firstPage: true);
                return;
            }

            ids.Clear();
            ids.AddRange(favorites.Ids);
            nextIdIndex = 0;
            models.Clear();
            anySaleShown = false;

            fetchCts = fetchCts.SafeRestartLinked(lifeCts.Token);
            LoadPageAsync(true, fetchCts.Token).Forget();
        }

        private void RequestNextPage()
        {
            if (!isActive || isLoadingPage || nextIdIndex >= ids.Count || fetchCts == null || fetchCts.IsCancellationRequested)
                return;

            LoadPageAsync(false, fetchCts.Token).Forget();
        }

        private async UniTaskVoid LoadPageAsync(bool firstPage, CancellationToken ct)
        {
            isLoadingPage = true;

            if (firstPage)
                view.SetLoadingPage(true);
            else
                view.SetLoadingMore(true);

            int countBefore = models.Count;

            // Ids the catalog no longer knows yield no card, so the read goes on until a page adds one or the ids run out
            while (models.Count == countBefore && nextIdIndex < ids.Count)
            {
                int end = Math.Min(nextIdIndex + PAGE_SIZE, ids.Count);
                pageIds.Clear();

                for (int i = nextIdIndex; i < end; i++)
                    pageIds.Add(ids[i]);

                Result<CatalogItemDto[]> result = await api.GetCatalogItemsByIdsAsync(pageIds, ct).SuppressToResultAsync(ReportCategory.UI);

                if (ct.IsCancellationRequested)
                    return;

                if (!result.Success)
                {
                    OnPageFailed(firstPage);
                    return;
                }

                nextIdIndex = end;

                foreach (CatalogItemDto row in result.Value)
                {
                    ShopItemCardModel model = ShopItemCardModel.FromCatalogItem(row);
                    string? key = ShopFavorites.KeyOf(model);

                    // A favorite removed while its page was loading must not come back
                    if (key != null && favorites.IsFavorite(key) && IndexOfKey(key) < 0)
                        models.Add(model);
                }
            }

            isLoadingPage = false;
            view.SetLoadingPage(false);
            view.SetLoadingMore(false);
            view.SetError(false);
            ShowModels(resetPosition: firstPage);
            ResolveCreatorNamesAsync(countBefore).Forget();
        }

        private void OnPageFailed(bool firstPage)
        {
            isLoadingPage = false;
            view.SetLoadingPage(false);
            view.SetLoadingMore(false);

            if (firstPage)
            {
                models.Clear();
                view.SetItemCount(0, true);
                view.ShowEmpty(false, EMPTY_MESSAGE);
                view.SetGridVisible(false);
                view.SetError(true);
            }

            NotificationsBusController.Instance.AddNotification(new ServerErrorNotification(LOAD_ERROR_MESSAGE));
        }

        private void OnFavoriteChanged(string key, ShopItemCardModel model, bool isFavorite)
        {
            if (!isActive || (isLoadingPage && models.Count == 0))
                return;

            int index = IndexOfKey(key);

            if (isFavorite)
            {
                if (index >= 0)
                    return;

                // The newest favorite leads the list; the ids already paged shift with it
                models.Insert(0, model);
                ids.Insert(0, key);
                nextIdIndex++;
            }
            else
            {
                if (index < 0)
                    return;

                models.RemoveAt(index);
                int idIndex = ids.FindIndex(id => string.Equals(id, key, StringComparison.OrdinalIgnoreCase));

                if (idIndex >= 0)
                {
                    ids.RemoveAt(idIndex);

                    if (idIndex < nextIdIndex)
                        nextIdIndex--;
                }
            }

            ShowModels(resetPosition: false);
        }

        private void ShowModels(bool resetPosition)
        {
            view.ShowEmpty(models.Count == 0, EMPTY_MESSAGE);
            view.SetGridVisible(models.Count > 0);
            view.SetItemCount(models.Count, resetPosition);
            view.RefreshAllShownItems();
        }

        private int IndexOfKey(string key)
        {
            for (var i = 0; i < models.Count; i++)
            {
                if (string.Equals(ShopFavorites.KeyOf(models[i]), key, StringComparison.OrdinalIgnoreCase))
                    return i;
            }

            return -1;
        }

        private async UniTaskVoid ResolveCreatorNamesAsync(int fromIndex)
        {
            if (lifeCts == null)
                return;

            creatorsScratch.Clear();
            creatorsSeen.Clear();

            for (int i = fromIndex; i < models.Count; i++)
            {
                if (creatorsSeen.Add(models[i].Creator))
                    creatorsScratch.Add(models[i].Creator);
            }

            if (creatorsScratch.Count == 0)
                return;

            creatorNamesCts = creatorNamesCts.SafeRestartLinked(lifeCts.Token);
            CancellationToken ct = creatorNamesCts.Token;
            bool changed = await creatorNames.ResolveAsync(creatorsScratch, ct);

            if (ct.IsCancellationRequested || !changed)
                return;

            view.ForEachShownCard(card =>
            {
                if (card.Model != null)
                    card.SetCreatorName(creatorNames.GetDisplayName(card.Model.Creator));
            });
        }

        private void OnBrowseClicked() =>
            BrowseClicked?.Invoke();

        private void OnCardAddToCartClicked(ShopItemCardView card)
        {
            if (actionsCts != null)
                cards.AddToCartAsync(card, ShopCartSource.Grid, actionsCts.Token).Forget();
        }

        private void OnCardBuyClicked(ShopItemCardView card)
        {
            if (actionsCts != null)
                cards.BuyAsync(card, CreditPurchaseModalControllerParams.SOURCE_SHOP_GRID, actionsCts.Token).Forget();
        }

        private void OnCardClicked(ShopItemCardView card)
        {
            if (card.Model != null)
                ItemClicked?.Invoke(card.Model);
        }

        private void OnListingResolved(ShopItemCardModel model, ShopListingDto listing)
        {
            int index = models.IndexOf(model);

            if (index < 0)
                return;

            ShopItemCardModel resolved = ShopItemCardModel.FromListing(listing);
            models[index] = resolved;
            anySaleShown |= resolved.IsSaleActive(NowUnixSeconds());
            view.RefreshItem(index);
        }

        private static long NowUnixSeconds() =>
            DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }
}
