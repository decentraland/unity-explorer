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
        private readonly PlaceCategoriesSO placesCategories;

        private PlacesFilters currentFilters = null!;
        private int currentPlacesPageNumber = 1;
        private bool isPlacesGridLoadingItems;
        private int currentPlacesTotalAmount;

        private CancellationTokenSource? loadPlacesCts;

        public PlacesResultsController(
            PlacesResultsView view,
            PlacesController placesController,
            IPlacesAPIService placesAPIService,
            PlacesStateService placesStateService,
            PlaceCategoriesSO placesCategories)
        {
            this.view = view;
            this.placesController = placesController;
            this.placesAPIService = placesAPIService;
            this.placesStateService = placesStateService;
            this.placesCategories = placesCategories;

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
            if (!string.IsNullOrEmpty(currentFilters.SearchText) || currentFilters.Section == PlacesSection.DISCOVER)
            {
                placesController.OpenSection(PlacesSection.DISCOVER, invokeEvent: false);
                loadPlacesCts = loadPlacesCts.SafeRestart();
                SearchPlacesAsync(pageNumber, loadPlacesCts.Token).Forget();
            }
            else
            {
                view.ClearPlacesResults();
                view.SetPlacesCounterActive(false);
            }
        }

        private async UniTask SearchPlacesAsync(int pageNumber, CancellationToken ct)
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

            bool isSearching = !string.IsNullOrEmpty(currentFilters.SearchText);

            var placesResult = await placesAPIService.SearchPlacesAsync(
                                                          pageNumber: pageNumber,
                                                          pageSize: PLACES_PER_PAGE,
                                                          ct: ct,
                                                          searchText: currentFilters.SearchText,
                                                          sortBy: currentFilters.SortBy,
                                                          sortDirection: IPlacesAPIService.SortDirection.DESC,
                                                          category: isSearching ? null : currentFilters.CategoryId)
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

                if (!string.IsNullOrEmpty(currentFilters.SearchText))
                    view.SetPlacesCounter($"Results for '{currentFilters.SearchText}' ({placesResult.Value.Total})");
                else if (currentFilters.Section == PlacesSection.DISCOVER)
                {
                    if (currentFilters.CategoryId != null)
                    {
                        string selectedCategoryName = placesCategories.GetCategoryName(currentFilters.CategoryId);
                        view.SetPlacesCounter($"Results for {(!string.IsNullOrEmpty(selectedCategoryName) ? selectedCategoryName : "the selected category")} ({placesResult.Value.Total})");
                    }
                    else
                        view.SetPlacesCounter($"Browse All Places ({placesResult.Value.Total})");
                }
                else if (currentFilters.Section == PlacesSection.RECENTLY_VISITED)
                    view.SetPlacesCounter($"Recently Visited ({placesResult.Value.Total})");
                else if (currentFilters.Section == PlacesSection.FAVORITES)
                    view.SetPlacesCounter($"Favorites ({placesResult.Value.Total})");
                else if (currentFilters.Section == PlacesSection.MY_PLACES)
                    view.SetPlacesCounter($"My Places ({placesResult.Value.Total})");
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
            loadPlacesCts?.SafeCancelAndDispose();
            view.ClearPlacesResults();
            placesStateService.ClearPlaces();
        }
    }
}
