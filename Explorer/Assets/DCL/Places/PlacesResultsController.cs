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

        private readonly PlacesResultsView placesResultsView;
        private readonly PlacesController placesController;
        private readonly IPlacesAPIService placesAPIService;
        private readonly PlacesStateService placesStateService;

        private PlacesSection? currentSection;
        private string? currentCategorySelected;
        private int currentPlacesPageNumber = 1;
        private bool isPlacesGridLoadingItems;
        private int currentPlacesTotalAmount;

        private CancellationTokenSource? getPlacesCts;

        public PlacesResultsController(
            PlacesResultsView placesResultsView,
            PlacesController placesController,
            IPlacesAPIService placesAPIService,
            PlacesStateService placesStateService)
        {
            this.placesResultsView = placesResultsView;
            this.placesController = placesController;
            this.placesAPIService = placesAPIService;
            this.placesStateService = placesStateService;

            placesController.SectionChanged += OnSectionChanged;
            placesController.PlacesClosed += UnloadPlaces;
            placesController.CategorySelected += OnCategorySelected;
            placesResultsView.PlacesGridScrollAtTheBottom += TryLoadMorePlaces;

            placesResultsView.SetDependencies(placesStateService);
            placesResultsView.InitializePlacesGrid();
        }

        public void Dispose()
        {
            placesController.SectionChanged -= OnSectionChanged;
            placesController.PlacesClosed -= UnloadPlaces;
            placesController.CategorySelected -= OnCategorySelected;
            placesResultsView.PlacesGridScrollAtTheBottom -= TryLoadMorePlaces;
        }

        private void OnSectionChanged(PlacesSection section)
        {
            if (currentSection == section)
                return;

            currentSection = section;
            currentCategorySelected = null;

            getPlacesCts = getPlacesCts.SafeRestart();
            LoadPlacesAsync(0, getPlacesCts.Token).Forget();
        }

        private void OnCategorySelected(string? categoryId)
        {
            if (currentCategorySelected == categoryId)
                return;

            currentCategorySelected = categoryId;

            getPlacesCts = getPlacesCts.SafeRestart();
            LoadPlacesAsync(0, getPlacesCts.Token).Forget();
        }

        private void TryLoadMorePlaces()
        {
            if (isPlacesGridLoadingItems || placesStateService.CurrentPlaces.Count >= currentPlacesTotalAmount)
                return;

            getPlacesCts = getPlacesCts.SafeRestart();
            LoadPlacesAsync(currentPlacesPageNumber + 1, getPlacesCts.Token).Forget();
        }

        private async UniTask LoadPlacesAsync(int pageNumber, CancellationToken ct)
        {
            isPlacesGridLoadingItems = true;

            if (pageNumber == 0)
            {
                placesStateService.ClearPlaces();
                placesResultsView.ClearPlacesResults();
                placesResultsView.SetPlacesGridAsLoading(true);
            }
            else
                placesResultsView.SetPlacesGridLoadingMoreActive(true);

            var placesResult = await placesAPIService.SearchPlacesAsync(
                                                          pageNumber: pageNumber,
                                                          pageSize: PLACES_PER_PAGE,
                                                          ct: ct,
                                                          searchText: null,
                                                          sortBy: IPlacesAPIService.SortBy.LIKE_SCORE,
                                                          sortDirection: IPlacesAPIService.SortDirection.DESC,
                                                          category: currentCategorySelected)
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
                placesResultsView.AddPlacesResultsItems(placesResult.Value.Data, pageNumber == 0);
            }

            currentPlacesTotalAmount = placesResult.Value.Total;

            if (pageNumber == 0)
                placesResultsView.SetPlacesGridAsLoading(false);

            placesResultsView.SetPlacesGridLoadingMoreActive(false);

            isPlacesGridLoadingItems = false;
        }

        private void UnloadPlaces()
        {
            getPlacesCts?.SafeCancelAndDispose();
            placesResultsView.ClearPlacesResults();
            placesStateService.ClearPlaces();

            currentCategorySelected = null;
        }
    }
}
