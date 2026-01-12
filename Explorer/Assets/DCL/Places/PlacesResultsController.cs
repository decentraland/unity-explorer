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
    public class PlacesResultsController : IDisposable
    {
        private const string GET_PLACES_ERROR_MESSAGE = "There was an error loading places. Please try again.";
        private const int PLACES_PER_PAGE = 20;

        private readonly PlacesResultsView view;
        private readonly PlacesController placesController;
        private readonly IPlacesAPIService placesAPIService;
        private readonly PlacesStateService placesStateService;

        private PlacesFilters currentFilters;
        private int currentPlacesPageNumber = 1;
        private bool isPlacesGridLoadingItems;
        private int currentPlacesTotalAmount;

        private CancellationTokenSource? getPlacesCts;

        public PlacesResultsController(
            PlacesResultsView view,
            PlacesController placesController,
            IPlacesAPIService placesAPIService,
            PlacesStateService placesStateService)
        {
            this.view = view;
            this.placesController = placesController;
            this.placesAPIService = placesAPIService;
            this.placesStateService = placesStateService;

            placesController.FiltersChanged += OnFiltersChanged;
            view.PlacesGridScrollAtTheBottom += TryLoadMorePlaces;
            placesController.PlacesClosed += UnloadPlaces;

            view.SetDependencies(placesStateService);
            view.InitializePlacesGrid();
        }

        public void Dispose()
        {
            placesController.FiltersChanged -= OnFiltersChanged;
            view.PlacesGridScrollAtTheBottom -= TryLoadMorePlaces;
            placesController.PlacesClosed -= UnloadPlaces;
        }

        private void OnFiltersChanged(PlacesFilters filters)
        {
            currentFilters = filters;
            LoadPlaces(0);
        }

        private void TryLoadMorePlaces()
        {
            if (isPlacesGridLoadingItems || placesStateService.CurrentPlaces.Count >= currentPlacesTotalAmount)
                return;

            LoadPlaces(currentPlacesPageNumber + 1);
        }

        private void LoadPlaces(int pageNumber)
        {
            if (currentFilters.Section == PlacesSection.DISCOVER)
            {
                getPlacesCts = getPlacesCts.SafeRestart();
                LoadPlacesAsync(pageNumber, getPlacesCts.Token).Forget();
            }
            else
            {
                view.ClearPlacesResults();
                view.SetPlacesCounterActive(false);
            }
        }

        private async UniTask LoadPlacesAsync(int pageNumber, CancellationToken ct)
        {
            isPlacesGridLoadingItems = true;

            if (pageNumber == 0)
            {
                placesStateService.ClearPlaces();
                view.ClearPlacesResults();
                view.SetPlacesGridAsLoading(true);
                view.SetPlacesCounterActive(false);
            }
            else
                view.SetPlacesGridLoadingMoreActive(true);

            var placesResult = await placesAPIService.SearchPlacesAsync(
                                                          pageNumber: pageNumber,
                                                          pageSize: PLACES_PER_PAGE,
                                                          ct: ct,
                                                          searchText: null,
                                                          sortBy: IPlacesAPIService.SortBy.LIKE_SCORE,
                                                          sortDirection: IPlacesAPIService.SortDirection.DESC,
                                                          category: currentFilters.CategoryId)
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
                currentPlacesPageNumber = pageNumber;
                placesStateService.AddPlaces(placesResult.Value.Data);
                view.AddPlacesResultsItems(placesResult.Value.Data, pageNumber == 0);
                view.SetPlacesCounter(placesResult.Value.Total);
            }

            view.SetPlacesCounterActive(placesResult.Value.Data.Count > 0);
            currentPlacesTotalAmount = placesResult.Value.Total;

            if (pageNumber == 0)
                view.SetPlacesGridAsLoading(false);

            view.SetPlacesGridLoadingMoreActive(false);

            isPlacesGridLoadingItems = false;
        }

        private void UnloadPlaces()
        {
            getPlacesCts?.SafeCancelAndDispose();
            view.ClearPlacesResults();
            placesStateService.ClearPlaces();
        }
    }
}
