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
    ///     One paged grid of a single item category (wearables or emotes); search and sort come from the shop header.
    /// </summary>
    public class ShopGridSectionController : IDisposable
    {
        private const string LOAD_ERROR_MESSAGE = "There was an error loading the shop. Please try again.";

        private readonly ShopGridSectionView view;
        private readonly MarketplaceShopAPIClient api;
        private readonly ShopItemCardPresenter cards;
        private readonly ShopCreatorCache creatorNames;
        private readonly List<ShopItemCardModel> models = new ();
        private readonly HashSet<string> modelKeys = new ();
        private readonly List<string> creatorsScratch = new ();
        private readonly HashSet<string> creatorsSeen = new ();

        private CancellationTokenSource? lifeCts;
        private CancellationTokenSource? fetchCts;
        private CancellationTokenSource? creatorNamesCts;
        private CancellationTokenSource? actionsCts;
        private bool isActive;
        private bool isLoadingPage;
        private bool hasMore;
        private bool anySaleOnPage;
        private int total;
        private int nextSkip;
        private string? lastSearchedQuery;
        private string? lastFilterSignature;
        private string? selectedKey;

        public ShopGridFilters Filters { get; }

        public event Action<string, int>? Searched;
        public event Action<ShopGridFilters, int>? FilterApplied;
        public event Action? BrowseClicked;
        public event Action<ShopItemCardModel>? ItemClicked;

        public ShopGridSectionController(
            ShopGridSectionView view,
            MarketplaceShopAPIClient api,
            ShopItemCardPresenter cards,
            ShopCreatorCache creatorNames,
            string category)
        {
            this.view = view;
            this.api = api;
            this.cards = cards;
            this.creatorNames = creatorNames;
            Filters = new ShopGridFilters(category);

            view.CategoryDropdown?.Initialize();
            view.InitializeGrid(ModelAt, BindCard);

            view.GridNearEnd += RequestNextPage;
            view.RetryClicked += RequestFirstPage;
            view.BrowseClicked += OnBrowseClicked;
            view.CardAddToCartClicked += OnCardAddToCartClicked;
            view.CardBuyClicked += OnCardBuyClicked;
            view.CardClicked += OnCardClicked;

            if (view.CategoryDropdown != null)
                view.CategoryDropdown.Selected += OnSubCategorySelected;

            cards.ListingResolved += OnListingResolved;
        }

        public void Dispose()
        {
            view.GridNearEnd -= RequestNextPage;
            view.RetryClicked -= RequestFirstPage;
            view.BrowseClicked -= OnBrowseClicked;
            view.CardAddToCartClicked -= OnCardAddToCartClicked;
            view.CardBuyClicked -= OnCardBuyClicked;
            view.CardClicked -= OnCardClicked;

            if (view.CategoryDropdown != null)
                view.CategoryDropdown.Selected -= OnSubCategorySelected;

            cards.ListingResolved -= OnListingResolved;

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
            view.CategoryDropdown?.SetSelected(Filters.SubCategoryKey);
            RequestFirstPage();
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

        public void ResetFilters()
        {
            Filters.Reset();
            lastSearchedQuery = null;
            lastFilterSignature = null;
            view.CategoryDropdown?.SetSelected(null);
        }

        public void SetSearch(string text)
        {
            if (Filters.SearchText == text)
                return;

            Filters.SearchText = text;
            RequestFirstPage();
        }

        public void SetSort(ShopSortOption sort)
        {
            if (Filters.Sort == sort)
                return;

            Filters.Sort = sort;
            RequestFirstPage();
        }

        public void SetCompact(bool compact) =>
            view.SetColumns(compact ? ShopGridSectionView.COMPACT_COLUMNS : ShopGridSectionView.DEFAULT_COLUMNS);

        public void SetSelected(string? key)
        {
            selectedKey = key;
            view.ForEachShownCard(card => card.SetSelected(card.Model != null && card.Model.Key == selectedKey));
        }

        public void Reload()
        {
            if (isActive)
                RequestFirstPage();
        }

        public void RefreshCartState() =>
            view.ForEachShownCard(cards.RefreshCartState);

        public void RefreshCountdowns(long nowUnixSeconds)
        {
            if (anySaleOnPage)
                view.ForEachShownCard(card => card.RefreshSaleCountdown(nowUnixSeconds));
        }

        private ShopItemCardModel? ModelAt(int index) =>
            index >= 0 && index < models.Count ? models[index] : null;

        private void BindCard(ShopItemCardView card, ShopItemCardModel model)
        {
            cards.Bind(card, model, NowUnixSeconds());
            card.SetSelected(model.Key == selectedKey);
        }

        private void RequestFirstPage()
        {
            if (!isActive || lifeCts == null)
                return;

            fetchCts = fetchCts.SafeRestartLinked(lifeCts.Token);
            LoadPageAsync(0, fetchCts.Token).Forget();
        }

        private void RequestNextPage()
        {
            if (!isActive || isLoadingPage || !hasMore || fetchCts == null || fetchCts.IsCancellationRequested)
                return;

            LoadPageAsync(nextSkip, fetchCts.Token).Forget();
        }

        private async UniTaskVoid LoadPageAsync(int skip, CancellationToken ct)
        {
            isLoadingPage = true;
            bool firstPage = skip == 0;

            if (firstPage)
                view.SetLoadingPage(true);
            else
                view.SetLoadingMore(true);

            ShopCatalogQuery query = ShopQueryMapper.ToQuery(Filters, skip);
            string? searchQuery = Filters.IsSearching ? Filters.SearchText.Trim() : null;
            List<ShopItemCardModel> page;
            int totalCount;

            if (Filters.UsesUnifiedFeed)
            {
                Result<ShopListingsResponse> result = await api.GetShopItemsAsync(query, ct).SuppressToResultAsync(ReportCategory.UI);

                if (ct.IsCancellationRequested)
                    return;

                if (!result.Success)
                {
                    OnPageFailed(firstPage);
                    return;
                }

                page = ToModels(result.Value.data);
                totalCount = result.Value.total;
            }
            else
            {
                Result<CatalogItemsResponse> result = await api.GetCatalogItemsAsync(query, ct).SuppressToResultAsync(ReportCategory.UI);

                if (ct.IsCancellationRequested)
                    return;

                if (!result.Success)
                {
                    OnPageFailed(firstPage);
                    return;
                }

                page = ToModels(result.Value.data);
                totalCount = result.Value.total;
            }

            if (firstPage)
            {
                models.Clear();
                modelKeys.Clear();
                anySaleOnPage = false;
            }

            long now = NowUnixSeconds();

            foreach (ShopItemCardModel model in page)
            {
                if (!modelKeys.Add(model.Key))
                    continue;

                models.Add(model);
                anySaleOnPage |= model.IsSaleActive(now);
            }

            total = totalCount;
            nextSkip = skip + query.First;
            hasMore = models.Count < total && page.Count > 0;
            isLoadingPage = false;

            view.SetLoadingPage(false);
            view.SetLoadingMore(false);
            view.SetError(false);
            view.SetEmpty(models.Count == 0, searchQuery);
            view.SetGridVisible(models.Count > 0);
            view.SetItemCount(models.Count, resetPosition: firstPage);

            if (firstPage)
            {
                view.RefreshAllShownItems();
                ReportAnalytics(searchQuery, total);
            }

            ResolveCreatorNamesAsync(page).Forget();
        }

        private void OnPageFailed(bool firstPage)
        {
            isLoadingPage = false;
            view.SetLoadingPage(false);
            view.SetLoadingMore(false);

            if (firstPage && models.Count == 0)
            {
                view.SetGridVisible(false);
                view.SetError(true);
            }
            else if (firstPage)
                view.SetGridVisible(true);

            NotificationsBusController.Instance.AddNotification(new ServerErrorNotification(LOAD_ERROR_MESSAGE));
        }

        private void ReportAnalytics(string? searchQuery, int resultCount)
        {
            if (searchQuery != null && searchQuery != lastSearchedQuery)
                Searched?.Invoke(searchQuery, resultCount);

            lastSearchedQuery = searchQuery;

            string signature = Filters.BuildAnalyticsSignature();

            if (lastFilterSignature != null && signature != lastFilterSignature)
                FilterApplied?.Invoke(Filters, resultCount);

            lastFilterSignature = signature;
        }

        private async UniTaskVoid ResolveCreatorNamesAsync(List<ShopItemCardModel> page)
        {
            if (lifeCts == null)
                return;

            creatorsScratch.Clear();
            creatorsSeen.Clear();

            foreach (ShopItemCardModel model in page)
            {
                if (creatorsSeen.Add(model.Creator))
                    creatorsScratch.Add(model.Creator);
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

        private void OnSubCategorySelected(string? subCategoryKey)
        {
            Filters.SubCategoryKey = subCategoryKey;
            RequestFirstPage();
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

            models[index] = ShopItemCardModel.FromListing(listing);
            view.RefreshItem(index);
        }

        private static List<ShopItemCardModel> ToModels(ShopListingDto[]? rows)
        {
            var list = new List<ShopItemCardModel>(rows?.Length ?? 0);

            if (rows == null)
                return list;

            foreach (ShopListingDto row in rows)
                list.Add(ShopItemCardModel.FromListing(row));

            return list;
        }

        private static List<ShopItemCardModel> ToModels(CatalogItemDto[]? rows)
        {
            var list = new List<ShopItemCardModel>(rows?.Length ?? 0);

            if (rows == null)
                return list;

            foreach (CatalogItemDto row in rows)
                list.Add(ShopItemCardModel.FromCatalogItem(row));

            return list;
        }

        private static long NowUnixSeconds() =>
            DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }
}
