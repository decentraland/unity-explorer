using Cysharp.Threading.Tasks;
using DCL.Browser;
using DCL.Diagnostics;
using DCL.MarketplaceCredits.Purchase;
using DCL.Multiplayer.Connections.DecentralandUrls;
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
    ///     The paged grid of the NAMEs their owners listed for sale. A card only selects its name; buying happens on the marketplace site.
    /// </summary>
    public class ShopNamesResaleController : IDisposable
    {
        /// <summary>
        ///     The marketplace sorts names on sale by date and by price only: the first two values of <see cref="ShopSortOption" />.
        /// </summary>
        public const int SORT_OPTION_COUNT = 2;

        private const int PAGE_SIZE = 48;
        private const string EMPTY_TEXT = "No NAMEs on sale.";
        private const string EMPTY_SEARCH_FORMAT = "No NAMEs on sale found for '{0}'.";
        private const string LOAD_ERROR_MESSAGE = "There was an error loading the NAMEs on sale. Please try again.";

        private readonly ShopNamesGridView view;
        private readonly MarketplaceShopAPIClient api;
        private readonly UnityAppWebBrowser webBrowser;
        private readonly IDecentralandUrlsSource urlsSource;
        private readonly List<ShopNameCardModel> models = new ();
        private readonly HashSet<string> modelKeys = new ();

        private CancellationTokenSource? lifeCts;
        private CancellationTokenSource? fetchCts;
        private bool isActive;
        private bool isLoadingPage;
        private bool hasMore;
        private int nextSkip;
        private string? selectedKey;

        public string SearchText { get; private set; } = string.Empty;

        public ShopSortOption Sort { get; private set; } = ShopSortOption.Newest;

        public event Action<string>? NameSelected;

        public ShopNamesResaleController(ShopNamesGridView view, MarketplaceShopAPIClient api, UnityAppWebBrowser webBrowser, IDecentralandUrlsSource urlsSource)
        {
            this.view = view;
            this.api = api;
            this.webBrowser = webBrowser;
            this.urlsSource = urlsSource;

            view.InitializeGrid(ModelAt, BindCard);

            view.GridNearEnd += RequestNextPage;
            view.RetryClicked += RequestFirstPage;
            view.CardClicked += OnCardClicked;
            view.CardViewClicked += OnCardViewClicked;
        }

        public void Dispose()
        {
            view.GridNearEnd -= RequestNextPage;
            view.RetryClicked -= RequestFirstPage;
            view.CardClicked -= OnCardClicked;
            view.CardViewClicked -= OnCardViewClicked;

            fetchCts.SafeCancelAndDispose();
            lifeCts.SafeCancelAndDispose();
        }

        public void Activate()
        {
            isActive = true;
            lifeCts = lifeCts.SafeRestart();
            RequestFirstPage();
        }

        public void Deactivate()
        {
            isActive = false;
            fetchCts.SafeCancelAndDispose();
            fetchCts = null;
            lifeCts.SafeCancelAndDispose();
            lifeCts = null;
            isLoadingPage = false;
            selectedKey = null;
            view.SetLoadingPage(false);
            view.SetLoadingMore(false);
        }

        public void SetSearch(string text)
        {
            if (string.Equals(SearchText, text, StringComparison.Ordinal))
                return;

            SearchText = text;
            RequestFirstPage();
        }

        public void SetSort(ShopSortOption sort)
        {
            if (Sort == sort)
                return;

            Sort = sort;
            RequestFirstPage();
        }

        /// <summary>
        ///     A search in the default order is ranked by how close each name is to the text; an explicit price order is kept.
        /// </summary>
        public static NamesSort ToNamesSort(ShopSortOption sort, bool isSearching)
        {
            if (sort == ShopSortOption.Cheapest)
                return NamesSort.Cheapest;

            return isSearching ? NamesSort.BestMatch : NamesSort.Newest;
        }

        private ShopNameCardModel? ModelAt(int index) =>
            index >= 0 && index < models.Count ? models[index] : null;

        private void BindCard(ShopNameCardView card, ShopNameCardModel model)
        {
            card.Bind(model);
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

            string? search = string.IsNullOrWhiteSpace(SearchText) ? null : SearchText.Trim();
            var query = new NamesOnSaleQuery { First = PAGE_SIZE, Skip = skip, Search = search, Sort = ToNamesSort(Sort, search != null) };

            Result<NameListingsResponse> result = await api.GetNamesOnSaleAsync(query, ct).SuppressToResultAsync(ReportCategory.UI);

            if (ct.IsCancellationRequested)
                return;

            if (!result.Success)
            {
                OnPageFailed(firstPage);
                return;
            }

            if (firstPage)
            {
                models.Clear();
                modelKeys.Clear();
            }

            NameListingDto[] rows = result.Value.data ?? Array.Empty<NameListingDto>();

            foreach (NameListingDto row in rows)
            {
                ShopNameCardModel? model = ShopNameCardModel.FromListing(row);

                if (model != null && modelKeys.Add(model.Key))
                    models.Add(model);
            }

            nextSkip = skip + PAGE_SIZE;
            hasMore = nextSkip < result.Value.total && rows.Length > 0;
            isLoadingPage = false;

            view.SetLoadingPage(false);
            view.SetLoadingMore(false);
            view.SetError(false);
            view.ShowEmpty(models.Count == 0, search != null ? string.Format(EMPTY_SEARCH_FORMAT, search) : EMPTY_TEXT);
            view.SetGridVisible(models.Count > 0);
            view.SetItemCount(models.Count, resetPosition: firstPage);

            if (firstPage)
                view.RefreshAllShownItems();
        }

        private void OnPageFailed(bool firstPage)
        {
            isLoadingPage = false;
            view.SetLoadingPage(false);
            view.SetLoadingMore(false);

            // The names on screen belong to the previous query, and paging on from them would mix the two.
            if (firstPage)
            {
                models.Clear();
                modelKeys.Clear();
                hasMore = false;
                view.ShowEmpty(false, EMPTY_TEXT);
                view.SetItemCount(0, resetPosition: true);
                view.SetGridVisible(false);
                view.SetError(true);
            }

            NotificationsBusController.Instance.AddNotification(new ServerErrorNotification(LOAD_ERROR_MESSAGE));
        }

        private void OnCardClicked(ShopNameCardView card)
        {
            if (card.Model == null)
                return;

            selectedKey = card.Model.Key;
            view.ForEachShownCard(shown => shown.SetSelected(shown.Model != null && shown.Model.Key == selectedKey));
            NameSelected?.Invoke(card.Model.Name);
        }

        private void OnCardViewClicked(ShopNameCardView card)
        {
            if (card.Model != null)
                webBrowser.OpenUrlMainThreadOnly($"{urlsSource.Url(DecentralandUrl.MarketplaceLink)}{card.Model.MarketplacePath}");
        }
    }
}
