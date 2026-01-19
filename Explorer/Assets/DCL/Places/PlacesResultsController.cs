using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.NotificationsBus;
using DCL.NotificationsBus.NotificationTypes;
using DCL.PlacesAPIService;
using DCL.Profiles;
using DCL.Profiles.Self;
using DCL.Utilities.Extensions;
using DCL.Utility.Types;
using System;
using System.Collections.Generic;
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
        private readonly ISelfProfile selfProfile;

        private PlacesFilters currentFilters = null!;
        private int currentPlacesPageNumber = 1;
        private bool isPlacesGridLoadingItems;
        private int currentPlacesTotalAmount;
        private PlacesSection sectionOpenedBeforeSearching = PlacesSection.DISCOVER;

        private CancellationTokenSource? loadPlacesCts;

        public PlacesResultsController(
            PlacesResultsView view,
            PlacesController placesController,
            IPlacesAPIService placesAPIService,
            PlacesStateService placesStateService,
            PlaceCategoriesSO placesCategories,
            ISelfProfile selfProfile)
        {
            this.view = view;
            this.placesController = placesController;
            this.placesAPIService = placesAPIService;
            this.placesStateService = placesStateService;
            this.placesCategories = placesCategories;
            this.selfProfile = selfProfile;

            view.BackButtonClicked += OnBackButtonClicked;
            view.PlacesGridScrollAtTheBottom += TryLoadMorePlaces;
            placesController.FiltersChanged += OnFiltersChanged;
            placesController.PlacesClosed += UnloadPlaces;

            view.SetDependencies(placesStateService);
            view.InitializePlacesGrid();
        }

        public void Dispose()
        {
            view.BackButtonClicked -= OnBackButtonClicked;
            view.PlacesGridScrollAtTheBottom -= TryLoadMorePlaces;
            placesController.FiltersChanged -= OnFiltersChanged;
            placesController.PlacesClosed -= UnloadPlaces;
        }

        private void OnBackButtonClicked() =>
            placesController.OpenSection(sectionOpenedBeforeSearching, force: true);

        private void TryLoadMorePlaces()
        {
            if (isPlacesGridLoadingItems || placesStateService.CurrentPlaces.Count >= currentPlacesTotalAmount)
                return;

            LoadPlaces(currentPlacesPageNumber + 1);
        }

        private void OnFiltersChanged(PlacesFilters filters)
        {
            currentFilters = filters;
            LoadPlaces(0);
        }

        private void LoadPlaces(int pageNumber)
        {
            PlacesSection sectionToLoad = currentFilters.Section!.Value;

            if (!string.IsNullOrEmpty(currentFilters.SearchText))
            {
                sectionOpenedBeforeSearching = currentFilters.Section!.Value;
                placesController.OpenSection(PlacesSection.DISCOVER, invokeEvent: false, cleanSearch: false);
                sectionToLoad = PlacesSection.DISCOVER;
            }

            loadPlacesCts = loadPlacesCts.SafeRestart();
            LoadPlacesAsync(pageNumber, sectionToLoad, loadPlacesCts.Token).Forget();
        }

        private async UniTask LoadPlacesAsync(int pageNumber, PlacesSection section, CancellationToken ct)
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

            Profile? ownProfile = await selfProfile.ProfileAsync(ct);
            if (ownProfile == null)
                return;

            Result<PlacesData.IPlacesAPIResponse> placesResult;

            switch (section)
            {
                case PlacesSection.DISCOVER:
                    placesResult = await placesAPIService.SearchPlacesAsync(
                                                              pageNumber: pageNumber, pageSize: PLACES_PER_PAGE, ct: ct,
                                                              searchText: currentFilters.SearchText, sortBy: currentFilters.SortBy, sortDirection: IPlacesAPIService.SortDirection.DESC,
                                                              category: !string.IsNullOrEmpty(currentFilters.SearchText) ? null : currentFilters.CategoryId)
                                                         .SuppressToResultAsync(ReportCategory.PLACES);
                    break;
                case PlacesSection.FAVORITES:
                    placesResult = await placesAPIService.GetFavoritesAsync(
                                                              ct: ct, pageNumber: pageNumber, pageSize: PLACES_PER_PAGE,
                                                              sortByBy: currentFilters.SortBy, sortDirection: IPlacesAPIService.SortDirection.DESC)
                                                         .SuppressToResultAsync(ReportCategory.PLACES);
                    break;
                case PlacesSection.MY_PLACES:
                    placesResult = await placesAPIService.GetWorldsByOwnerAsync(ownerAddress: ownProfile.UserId, ct: ct)
                                                         .SuppressToResultAsync(ReportCategory.PLACES);
                    break;
                case PlacesSection.RECENTLY_VISITED:
                    var recentlyVisitedPlacesIds = placesAPIService.GetRecentlyVisitedPlaces();
                    var placesByIdResult = await placesAPIService.GetPlacesByIdsAsync(recentlyVisitedPlacesIds, ct)
                                                                 .SuppressToResultAsync(ReportCategory.PLACES);

                    // Since GetPlacesByIds endpoint doesn't return the data with the same sorting as the input list, we have to sort it manually
                    PlacesData.PlacesAPIResponse sortedPlacesResponse = GetRecentlyVisitedPlacesSorted(placesByIdResult, recentlyVisitedPlacesIds);
                    placesResult = await UniTask.FromResult<PlacesData.IPlacesAPIResponse>(sortedPlacesResponse)
                                                .SuppressToResultAsync(ReportCategory.PLACES);

                    break;
                default:
                    PlacesData.PlacesAPIResponse emptyPlacesResponse = new PlacesData.PlacesAPIResponse { data = new List<PlacesData.PlaceInfo>(), total = 0 };
                    placesResult = await UniTask.FromResult<PlacesData.IPlacesAPIResponse>(emptyPlacesResponse)
                                                .SuppressToResultAsync(ReportCategory.PLACES);
                    break;
            }

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
                    view.SetPlacesCounter($"Results for '{currentFilters.SearchText}' ({placesResult.Value.Total})", showBackButton: true);
                else switch (currentFilters.Section)
                {
                    case PlacesSection.DISCOVER when currentFilters.CategoryId != null:
                    {
                        string selectedCategoryName = placesCategories.GetCategoryName(currentFilters.CategoryId);
                        view.SetPlacesCounter($"Results for {(!string.IsNullOrEmpty(selectedCategoryName) ? selectedCategoryName : "the selected category")} ({placesResult.Value.Total})");
                        break;
                    }
                    case PlacesSection.DISCOVER:
                        view.SetPlacesCounter($"Browse All Places ({placesResult.Value.Total})"); break;
                    case PlacesSection.RECENTLY_VISITED:
                        view.SetPlacesCounter($"Recently Visited ({placesResult.Value.Total})"); break;
                    case PlacesSection.FAVORITES:
                        view.SetPlacesCounter($"Favorites ({placesResult.Value.Total})"); break;
                    case PlacesSection.MY_PLACES:
                        view.SetPlacesCounter($"My Places ({placesResult.Value.Total})"); break;
                }
            }

            view.SetPlacesCounterActive(placesResult.Value.Data.Count > 0);
            currentPlacesTotalAmount = placesResult.Value.Total;

            if (pageNumber == 0)
                view.SetPlacesGridAsLoading(false);

            view.SetPlacesGridLoadingMoreActive(false);

            isPlacesGridLoadingItems = false;
        }

        private static PlacesData.PlacesAPIResponse GetRecentlyVisitedPlacesSorted(Result<PlacesData.IPlacesAPIResponse> placesResult, List<string> sortedPlacesIds)
        {
            PlacesData.PlacesAPIResponse sortedPlacesResponse = new PlacesData.PlacesAPIResponse { data = new List<PlacesData.PlaceInfo>(), total = 0 };

            if (!placesResult.Success)
                return sortedPlacesResponse;

            foreach (string placeId in sortedPlacesIds)
            {
                foreach (PlacesData.PlaceInfo placeInfo in placesResult.Value.Data)
                {
                    if (placeInfo.id != placeId)
                        continue;

                    sortedPlacesResponse.data.Add(placeInfo);
                    break;
                }
            }

            sortedPlacesResponse.total = sortedPlacesResponse.data.Count;

            return sortedPlacesResponse;
        }

        private void UnloadPlaces()
        {
            loadPlacesCts?.SafeCancelAndDispose();
            view.ClearPlacesResults();
            placesStateService.ClearPlaces();
        }
    }
}
