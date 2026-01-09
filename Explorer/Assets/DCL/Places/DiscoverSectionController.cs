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
        private string? currentCategorySelected;
        private int currentPlacesPageNumber = 1;
        private bool isPlacesGridLoadingItems;
        private int currentPlacesTotalAmount;

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
            view.CategorySelected += OnCategorySelected;
            view.PlacesGridScrollAtTheBottom += TryLoadMorePlaces;

            view.SetDependencies(placesStateService);
            view.InitializePlacesGrid();
        }

        public void Dispose()
        {
            placesController.SectionChanged -= OnSectionChanged;
            placesController.PlacesClosed -= OnPlacesSectionClosed;
            view.CategorySelected -= OnCategorySelected;
            view.PlacesGridScrollAtTheBottom -= TryLoadMorePlaces;

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
            view.ClearCategories();

            getPlacesCts?.SafeCancelAndDispose();
            view.ClearPlacesResults();
            placesStateService.ClearPlaces();

            currentCategorySelected = null;
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
                view.AddPlacesResultsItems(placesResult.Value.Data, pageNumber == 0);
            }

            currentPlacesTotalAmount = placesResult.Value.Total;

            if (pageNumber == 0)
                view.SetPlacesGridAsLoading(false);

            view.SetPlacesGridLoadingMoreActive(false);

            isPlacesGridLoadingItems = false;
        }
    }
}
