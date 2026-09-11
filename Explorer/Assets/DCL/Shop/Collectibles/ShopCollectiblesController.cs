using Cysharp.Threading.Tasks;
using DCL.Backpack.Gifting.Styling;
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
    public class ShopCollectiblesController : IDisposable
    {
        private const string LOAD_ERROR_MESSAGE = "There was an error loading the shop. Please try again.";
        private const string CHIP_PRICE = "price";
        private const string CHIP_SMART = "smart";
        private const string CHIP_STATUS = "status";
        private const string CHIP_SMART_LABEL = "Smart";
        private const string CHIP_PRICE_FORMAT = "Price: {0} - {1}";
        private const string CHIP_PRICE_MIN_FORMAT = "Price: from {0}";
        private const string CHIP_PRICE_MAX_FORMAT = "Price: up to {0}";

        private readonly ShopCollectiblesView view;
        private readonly ShopFiltersView filtersView;
        private readonly MarketplaceShopAPIClient api;
        private readonly ShopItemCardPresenter cards;
        private readonly ShopCreatorNameCache creatorNames;
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
        private bool firstLoadDone;
        private bool anySaleOnPage;
        private int total;
        private int nextSkip;
        private string? lastSearchedQuery;
        private string? lastFilterSignature;

        public ShopCollectiblesFilters Filters { get; } = new ();

        public event Action<string, int>? Searched;
        public event Action<ShopCollectiblesFilters, int>? FilterApplied;
        public event Action? BackToOverviewClicked;

        public ShopCollectiblesController(
            ShopCollectiblesView view,
            ShopFiltersView filtersView,
            MarketplaceShopAPIClient api,
            ShopItemCardPresenter cards,
            ShopCreatorNameCache creatorNames,
            IWearableStylingCatalog styling)
        {
            this.view = view;
            this.filtersView = filtersView;
            this.api = api;
            this.cards = cards;
            this.creatorNames = creatorNames;

            filtersView.CategoryIconResolver = styling.GetCategoryIcon;
            filtersView.RarityColorResolver = styling.GetRarityFlapColor;
            view.InitializeGrid(ModelAt, BindCard);

            view.SearchChanged += OnSearchChanged;
            view.SortChanged += OnSortChanged;
            view.ChipRemoveClicked += OnChipRemoveClicked;
            view.ClearAllClicked += OnClearAllClicked;
            view.GridNearEnd += RequestNextPage;
            view.RetryClicked += RequestFirstPage;
            view.BackToOverviewClicked += OnBackToOverviewClicked;
            view.CardAddToCartClicked += OnCardAddToCartClicked;
            view.CardBuyClicked += OnCardBuyClicked;
            view.CardViewClicked += OnCardViewClicked;

            filtersView.CategorySelected += OnCategorySelected;
            filtersView.SubCategorySelected += OnSubCategorySelected;
            filtersView.RarityToggled += OnRarityToggled;
            filtersView.PriceRangeChanged += OnPriceRangeChanged;
            filtersView.StatusChanged += OnStatusChanged;
            filtersView.SmartChanged += OnSmartChanged;

            cards.ListingResolved += OnListingResolved;
        }

        public void Dispose()
        {
            view.SearchChanged -= OnSearchChanged;
            view.SortChanged -= OnSortChanged;
            view.ChipRemoveClicked -= OnChipRemoveClicked;
            view.ClearAllClicked -= OnClearAllClicked;
            view.GridNearEnd -= RequestNextPage;
            view.RetryClicked -= RequestFirstPage;
            view.BackToOverviewClicked -= OnBackToOverviewClicked;
            view.CardAddToCartClicked -= OnCardAddToCartClicked;
            view.CardBuyClicked -= OnCardBuyClicked;
            view.CardViewClicked -= OnCardViewClicked;

            filtersView.CategorySelected -= OnCategorySelected;
            filtersView.SubCategorySelected -= OnSubCategorySelected;
            filtersView.RarityToggled -= OnRarityToggled;
            filtersView.PriceRangeChanged -= OnPriceRangeChanged;
            filtersView.StatusChanged -= OnStatusChanged;
            filtersView.SmartChanged -= OnSmartChanged;

            cards.ListingResolved -= OnListingResolved;

            fetchCts.SafeCancelAndDispose();
            creatorNamesCts.SafeCancelAndDispose();
            actionsCts.SafeCancelAndDispose();
            lifeCts.SafeCancelAndDispose();
        }

        public void Activate()
        {
            isActive = true;
            lifeCts = lifeCts.SafeRestart();
            actionsCts = actionsCts.SafeRestartLinked(lifeCts.Token);
            ApplyFiltersToViews();
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
            view.SetRefreshing(false);
            view.SetLoadingMore(false);
        }

        public void ResetFilters()
        {
            Filters.Reset();
            lastSearchedQuery = null;
            lastFilterSignature = null;
            ApplyFiltersToViews();
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

        private void BindCard(ShopItemCardView card, ShopItemCardModel model) =>
            cards.Bind(card, model, NowUnixSeconds());

        private void OnAnyFilterChanged()
        {
            ApplyChips();
            RequestFirstPage();
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
            {
                if (!firstLoadDone && models.Count == 0)
                    view.SetFirstLoadSkeleton(true);
                else
                    view.SetRefreshing(true);
            }
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
            firstLoadDone = true;
            isLoadingPage = false;

            view.SetFirstLoadSkeleton(false);
            view.SetRefreshing(false);
            view.SetLoadingMore(false);
            view.SetError(false);
            view.SetEmpty(models.Count == 0, searchQuery);
            view.SetGridVisible(models.Count > 0);
            view.SetCounter(total, searchQuery);
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
            view.SetFirstLoadSkeleton(false);
            view.SetRefreshing(false);
            view.SetLoadingMore(false);

            if (firstPage && models.Count == 0)
            {
                view.SetGridVisible(false);
                view.SetError(true);
            }

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

        private void ApplyFiltersToViews()
        {
            filtersView.ApplyState(Filters);
            view.SetSort(Filters.Sort);
            view.SetSearchText(Filters.SearchText);
            ApplyChips();
        }

        private void ApplyChips()
        {
            view.ClearChips();
            var any = false;

            if (Filters.HasPriceRange)
            {
                view.AddChip(CHIP_PRICE, PriceChipLabel());
                any = true;
            }

            foreach (string rarity in Filters.Rarities)
            {
                view.AddChip(rarity, Capitalize(rarity));
                any = true;
            }

            if (Filters.Smart)
            {
                view.AddChip(CHIP_SMART, CHIP_SMART_LABEL);
                any = true;
            }

            if (Filters.IsStatusChipVisible)
            {
                view.AddChip(CHIP_STATUS, StatusLabel(Filters.EffectiveStatus));
                any = true;
            }

            view.SetClearAllVisible(any);
        }

        private string PriceChipLabel()
        {
            if (Filters.MinPriceCredits.HasValue && Filters.MaxPriceCredits.HasValue)
                return string.Format(CHIP_PRICE_FORMAT, Filters.MinPriceCredits.Value, Filters.MaxPriceCredits.Value);

            return Filters.MinPriceCredits.HasValue
                ? string.Format(CHIP_PRICE_MIN_FORMAT, Filters.MinPriceCredits.Value)
                : string.Format(CHIP_PRICE_MAX_FORMAT, Filters.MaxPriceCredits!.Value);
        }

        private static string StatusLabel(ShopStatusFilter status) =>
            status switch
            {
                ShopStatusFilter.All => "All",
                ShopStatusFilter.NotForSale => "Not for sale",
                _ => "On sale",
            };

        private static string Capitalize(string value) =>
            value.Length == 0 ? value : char.ToUpperInvariant(value[0]) + value.Substring(1);

        private void OnSearchChanged(string text)
        {
            Filters.SearchText = text;
            filtersView.ApplyState(Filters);
            OnAnyFilterChanged();
        }

        private void OnSortChanged(ShopSortOption sort)
        {
            Filters.Sort = sort;
            OnAnyFilterChanged();
        }

        private void OnCategorySelected(string category)
        {
            Filters.Category = category;
            Filters.SubCategoryKey = null;
            OnAnyFilterChanged();
        }

        private void OnSubCategorySelected(string? subCategoryKey)
        {
            Filters.SubCategoryKey = subCategoryKey;
            OnAnyFilterChanged();
        }

        private void OnRarityToggled(string rarity, bool isOn)
        {
            bool contains = Filters.Rarities.Contains(rarity);

            if (isOn == contains)
                return;

            Filters.ToggleRarity(rarity);
            OnAnyFilterChanged();
        }

        private void OnPriceRangeChanged(int? min, int? max)
        {
            Filters.MinPriceCredits = min;
            Filters.MaxPriceCredits = max;
            OnAnyFilterChanged();
        }

        private void OnStatusChanged(ShopStatusFilter status)
        {
            Filters.ExplicitStatus = status;
            OnAnyFilterChanged();
        }

        private void OnSmartChanged(bool isOn)
        {
            Filters.Smart = isOn;
            OnAnyFilterChanged();
        }

        private void OnChipRemoveClicked(ShopFilterChipView chip)
        {
            switch (chip.Key)
            {
                case CHIP_PRICE:
                    Filters.MinPriceCredits = null;
                    Filters.MaxPriceCredits = null;
                    break;
                case CHIP_SMART:
                    Filters.Smart = false;
                    break;
                case CHIP_STATUS:
                    Filters.ExplicitStatus = null;
                    break;
                default:
                    Filters.Rarities.Remove(chip.Key);
                    break;
            }

            filtersView.ApplyState(Filters);
            OnAnyFilterChanged();
        }

        private void OnClearAllClicked()
        {
            Filters.ClearFilters();
            filtersView.ApplyState(Filters);
            view.SetSort(Filters.Sort);
            OnAnyFilterChanged();
        }

        private void OnBackToOverviewClicked() =>
            BackToOverviewClicked?.Invoke();

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

        private void OnCardViewClicked(ShopItemCardView card)
        {
            if (card.Model != null)
                cards.View(card.Model);
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
