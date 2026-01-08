using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.NotificationsBus;
using DCL.NotificationsBus.NotificationTypes;
using DCL.PlacesAPIService;
using DCL.Utilities.Extensions;
using System;
using System.Threading;
using Utility;

namespace DCL.Places
{
    public class DiscoverSectionController : IDisposable
    {
        private const string GET_CATEGORIES_ERROR_MESSAGE = "There was an error loading categories. Please try again.";
        private const string GET_PLACES_ERROR_MESSAGE = "There was an error loading places. Please try again.";
        private const int PLACES_PER_PAGE = 20;

        private readonly DiscoverSectionView view;
        private readonly PlacesController placesController;
        private readonly IPlacesAPIService placesAPIService;
        private readonly PlacesStateService placesStateService;

        private bool isSectionOpen;
        private int currentPageNumberFilter = 1;
        private bool isGridResultsLoadingItems;

        private CancellationTokenSource? getCategoriesCts;
        private CancellationTokenSource? getPlacesCts;

        public DiscoverSectionController(
            PlacesController placesController,
            DiscoverSectionView view,
            IPlacesAPIService placesAPIService,
            PlacesStateService placesStateService)
        {
            this.view = view;
            this.placesController = placesController;
            this.placesAPIService = placesAPIService;
            this.placesStateService = placesStateService;

            placesController.SectionChanged += OnSectionChanged;
            placesController.PlacesClosed += OnPlacesSectionClosed;

            view.SetDependencies(placesStateService);
            view.InitializePlacesGrid();
        }

        public void Dispose()
        {
            placesController.SectionChanged -= OnSectionChanged;
            UnloadSection();
        }

        private void OnSectionChanged(PlacesSections? fromSection, PlacesSections toSection)
        {
            if (toSection == PlacesSections.DISCOVER)
            {
                LoadSection();
                isSectionOpen = true;
            }
            else if (fromSection == PlacesSections.DISCOVER)
            {
                UnloadSection();
                isSectionOpen = false;
            }
        }

        private void OnPlacesSectionClosed()
        {
            if (!isSectionOpen)
                return;

            UnloadSection();
        }

        private void LoadSection()
        {
            getCategoriesCts = getCategoriesCts.SafeRestart();
            LoadCategoriesAsync(getCategoriesCts.Token).Forget();

            getPlacesCts = getPlacesCts.SafeRestart();
            LoadPlacesAsync(0, getPlacesCts.Token).Forget();
        }

        private void UnloadSection()
        {
            getCategoriesCts?.SafeCancelAndDispose();
            getPlacesCts?.SafeCancelAndDispose();
            view.ClearCategories();
            view.ClearPlacesResults();
        }

        private async UniTask LoadCategoriesAsync(CancellationToken ct)
        {
            view.ClearCategories();

            var categoriesResult = await placesAPIService.GetPlacesCategoriesAsync(ct)
                                                         .SuppressToResultAsync(ReportCategory.PLACES);

            if (!categoriesResult.Success)
            {
                NotificationsBusController.Instance.AddNotification(new ServerErrorNotification(GET_CATEGORIES_ERROR_MESSAGE));
                return;
            }

            view.SetCategories(categoriesResult.Value.data);
        }

        private async UniTask LoadPlacesAsync(int pageNumber, CancellationToken ct)
        {
            isGridResultsLoadingItems = true;

            if (pageNumber == 0)
            {
                view.ClearPlacesResults();
                view.SetPlacesGridAsLoading(true);
            }
            else
                view.SetPlacesGridLoadingMoreActive(true);

            var placesResult = await placesAPIService.SearchPlacesAsync(
                                                          pageNumber: pageNumber,
                                                          pageSize: PLACES_PER_PAGE,
                                                          ct: ct,
                                                          searchText: null,
                                                          sortBy: IPlacesAPIService.SortBy.MOST_ACTIVE,
                                                          sortDirection: IPlacesAPIService.SortDirection.DESC,
                                                          category: null)
                                                     .SuppressToResultAsync(ReportCategory.PLACES);

            if (ct.IsCancellationRequested)
                return;

            if (!placesResult.Success)
            {
                NotificationsBusController.Instance.AddNotification(new ServerErrorNotification(GET_PLACES_ERROR_MESSAGE));
                return;
            }

            if (placesResult.Value.Data.Count > 0)
            {
                currentPageNumberFilter = pageNumber;
                placesStateService.AddPlaces(placesResult.Value.Data);
                view.AddPlacesResultsItems(placesResult.Value.Data, pageNumber == 0);
            }

            if (pageNumber == 0)
                view.SetPlacesGridAsLoading(false);

            view.SetPlacesGridLoadingMoreActive(false);

            isGridResultsLoadingItems = false;
        }
    }
}
