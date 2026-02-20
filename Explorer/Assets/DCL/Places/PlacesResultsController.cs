using Cysharp.Threading.Tasks;
using DCL.Browser;
using DCL.Communities;
using DCL.Diagnostics;
using DCL.EventsApi;
using DCL.Friends;
using DCL.MapRenderer.MapLayers.HomeMarker;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.NotificationsBus;
using DCL.NotificationsBus.NotificationTypes;
using DCL.PlacesAPIService;
using DCL.Profiles;
using DCL.Profiles.Self;
using DCL.UI.Profiles.Helpers;
using DCL.Utilities;
using DCL.Utilities.Extensions;
using DCL.Utility.Types;
using MVC;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Utility;

namespace DCL.Places
{
    public class PlacesResultsController : IDisposable
    {
        public event Action<string, int>? PlacesSearched;
        public event Action<PlacesFilters>? PlacesFiltered;
        public event Action<PlacesData.PlaceInfo, PlaceCardView, int, PlacesFilters>? PlaceClicked;

        private const string GET_FRIENDS_ERROR_MESSAGE = "There was an error loading friends. Please try again.";
        private const string GET_LIVE_EVENTS_ERROR_MESSAGE = "There was an error getting live events. Please try again.";
        private const string GET_PLACES_ERROR_MESSAGE = "There was an error loading places. Please try again.";
        private const int PLACES_PER_PAGE = 20;
        private const string SEARCH_COUNTER_TITLE = "Results for '{0}' {1}";
        private const string RECENT_VISITED_COUNTER_TITLE = "Recent {0}";
        private const string FAVORITES_COUNTER_TITLE = "Favorites {0}";
        private const string MY_PLACES_COUNTER_TITLE = "My Places {0}";

        private readonly PlacesResultsView view;
        private readonly PlacesController placesController;
        private readonly IPlacesAPIService placesAPIService;
        private readonly PlacesStateService placesStateService;
        private readonly ISelfProfile selfProfile;
        private readonly IWebBrowser webBrowser;
        private readonly PlacesCardSocialActionsController placesCardSocialActionsController;
        private readonly ObjectProxy<IFriendsService> friendServiceProxy;
        private readonly IMVCManager mvcManager;
        private readonly HttpEventsApiService eventsApiService;

        private PlacesFilters currentFilters = null!;
        private int currentPlacesPageNumber = 1;
        private bool isPlacesGridLoadingItems;
        private int currentPlacesTotalAmount;
        private PlacesSection sectionOpenedBeforeSearching = PlacesSection.BROWSE;
        private bool allFriendsLoaded;
        private bool liveEventsLoaded;

        private CancellationTokenSource? loadPlacesCts;
        private CancellationTokenSource? placeCardOperationsCts;

        public PlacesResultsController(
            PlacesResultsView view,
            PlacesController placesController,
            IPlacesAPIService placesAPIService,
            PlacesStateService placesStateService,
            ISelfProfile selfProfile,
            IWebBrowser webBrowser,
            ObjectProxy<IFriendsService> friendServiceProxy,
            ProfileRepositoryWrapper profileRepositoryWrapper,
            IMVCManager mvcManager,
            ThumbnailLoader thumbnailLoader,
            PlacesCardSocialActionsController placesCardSocialActionsController,
            HomePlaceEventBus homePlaceEventBus,
            HttpEventsApiService eventsApiService)
        {
            this.view = view;
            this.placesController = placesController;
            this.placesAPIService = placesAPIService;
            this.placesStateService = placesStateService;
            this.selfProfile = selfProfile;
            this.webBrowser = webBrowser;
            this.friendServiceProxy = friendServiceProxy;
            this.mvcManager = mvcManager;
            this.placesCardSocialActionsController = placesCardSocialActionsController;
            this.eventsApiService = eventsApiService;

            view.BackButtonClicked += OnBackButtonClicked;
            view.ExplorePlacesClicked += OnExplorePlacesClicked;
            view.GetANameClicked += GetANameClicked;
            view.PlacesGridScrollAtTheBottom += TryLoadMorePlaces;
            view.PlaceLikeToggleChanged += OnPlaceLikeToggleChanged;
            view.PlaceDislikeToggleChanged += OnPlaceDislikeToggleChanged;
            view.PlaceFavoriteToggleChanged += OnPlaceFavoriteToggleChanged;
            view.PlaceHomeToggleChanged += OnPlaceHomeToggleChanged;
            view.PlaceJumpInButtonClicked += OnPlaceJumpInButtonClicked;
            view.PlaceShareButtonClicked += OnPlaceShareButtonClicked;
            view.PlaceCopyLinkButtonClicked += OnPlaceCopyLinkButtonClicked;
            view.MainButtonClicked += OnMainButtonClicked;
            placesController.FiltersChanged += OnFiltersChanged;
            placesController.PlacesClosed += UnloadPlaces;
            placesCardSocialActionsController.PlaceSetAsHome += OnPlaceSetAsHome;

            view.SetDependencies(placesStateService, thumbnailLoader, profileRepositoryWrapper, homePlaceEventBus);
            view.InitializePlacesGrid();
        }

        public void Dispose()
        {
            view.BackButtonClicked -= OnBackButtonClicked;
            view.ExplorePlacesClicked -= OnExplorePlacesClicked;
            view.GetANameClicked -= GetANameClicked;
            view.PlacesGridScrollAtTheBottom -= TryLoadMorePlaces;
            view.PlaceLikeToggleChanged -= OnPlaceLikeToggleChanged;
            view.PlaceDislikeToggleChanged -= OnPlaceDislikeToggleChanged;
            view.PlaceFavoriteToggleChanged -= OnPlaceFavoriteToggleChanged;
            view.PlaceHomeToggleChanged -= OnPlaceHomeToggleChanged;
            view.PlaceJumpInButtonClicked -= OnPlaceJumpInButtonClicked;
            view.PlaceShareButtonClicked -= OnPlaceShareButtonClicked;
            view.PlaceCopyLinkButtonClicked -= OnPlaceCopyLinkButtonClicked;
            view.MainButtonClicked -= OnMainButtonClicked;
            placesController.FiltersChanged -= OnFiltersChanged;
            placesController.PlacesClosed -= UnloadPlaces;
            placesCardSocialActionsController.PlaceSetAsHome -= OnPlaceSetAsHome;

            loadPlacesCts?.SafeCancelAndDispose();
            placeCardOperationsCts.SafeCancelAndDispose();
        }

        private void OnBackButtonClicked() =>
            placesController.OpenSection(sectionOpenedBeforeSearching, force: true);

        private void OnExplorePlacesClicked() =>
            placesController.OpenSection(PlacesSection.BROWSE, force: true, resetCategory: true);

        private void GetANameClicked() =>
            webBrowser.OpenUrl(DecentralandUrl.MarketplaceClaimName);

        private void TryLoadMorePlaces()
        {
            if (isPlacesGridLoadingItems || placesStateService.CurrentPlaces.Count >= currentPlacesTotalAmount)
                return;

            LoadPlaces(currentPlacesPageNumber + 1);
        }

        private void OnPlaceLikeToggleChanged(PlacesData.PlaceInfo placeInfo, bool likeValue, PlaceCardView placeCardView)
        {
            placeCardOperationsCts = placeCardOperationsCts.SafeRestart();
            placesCardSocialActionsController.LikePlaceAsync(placeInfo, likeValue, placeCardView, null, placeCardOperationsCts.Token).Forget();
        }

        private void OnPlaceDislikeToggleChanged(PlacesData.PlaceInfo placeInfo, bool dislikeValue, PlaceCardView placeCardView)
        {
            placeCardOperationsCts = placeCardOperationsCts.SafeRestart();
            placesCardSocialActionsController.DislikePlaceAsync(placeInfo, dislikeValue, placeCardView, null, placeCardOperationsCts.Token).Forget();
        }

        private void OnPlaceFavoriteToggleChanged(PlacesData.PlaceInfo placeInfo, bool favoriteValue, PlaceCardView placeCardView)
        {
            placeCardOperationsCts = placeCardOperationsCts.SafeRestart();
            placesCardSocialActionsController.UpdateFavoritePlaceAsync(placeInfo, favoriteValue, placeCardView, null, placeCardOperationsCts.Token).Forget();
        }

        private void OnPlaceHomeToggleChanged(PlacesData.PlaceInfo placeInfo, bool homeValue, PlaceCardView placeCardView) =>
            placesCardSocialActionsController.SetPlaceAsHome(placeInfo, homeValue, placeCardView, null);

        private void OnPlaceJumpInButtonClicked(PlacesData.PlaceInfo placeInfo)
        {
            placeCardOperationsCts = placeCardOperationsCts.SafeRestart();
            placesCardSocialActionsController.JumpInPlace(placeInfo, placeCardOperationsCts.Token);
        }

        private void OnPlaceShareButtonClicked(PlacesData.PlaceInfo placeInfo) =>
            placesCardSocialActionsController.SharePlace(placeInfo);

        private void OnPlaceCopyLinkButtonClicked(PlacesData.PlaceInfo placeInfo) =>
            placesCardSocialActionsController.CopyPlaceLink(placeInfo);

        private void OnMainButtonClicked(PlacesData.PlaceInfo placeInfo, PlaceCardView placeCardView)
        {
            var placeInfoWithConnectedFriends = placesStateService.GetPlaceInfoById(placeInfo.id);
            mvcManager.ShowAsync(PlaceDetailPanelController.IssueCommand(new PlaceDetailPanelParameter(placeInfo, placeCardView, placeInfoWithConnectedFriends.ConnectedFriends, placeInfoWithConnectedFriends.LiveEvent))).Forget();
            PlaceClicked?.Invoke(placeInfo, placeCardView, currentPlacesTotalAmount, currentFilters);
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
                placesController.OpenSection(PlacesSection.BROWSE, invokeEvent: false, cleanSearch: false);
                sectionToLoad = PlacesSection.BROWSE;
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
                view.ClearPlacesResults(currentFilters.Section);
                view.SetPlacesGridAsLoading(true);
                view.SetPlacesCounterActive(currentFilters.Section != PlacesSection.BROWSE || !string.IsNullOrEmpty(currentFilters.SearchText));

                if (!string.IsNullOrEmpty(currentFilters.SearchText))
                    view.SetPlacesCounter(string.Format(SEARCH_COUNTER_TITLE, currentFilters.SearchText, string.Empty), showBackButton: true);
                else
                    switch (currentFilters.Section)
                    {
                        case PlacesSection.RECENTLY_VISITED:
                            view.SetPlacesCounter(string.Format(RECENT_VISITED_COUNTER_TITLE, string.Empty));
                            break;
                        case PlacesSection.FAVORITES:
                            view.SetPlacesCounter(string.Format(FAVORITES_COUNTER_TITLE, string.Empty));
                            break;
                        case PlacesSection.MY_PLACES:
                            view.SetPlacesCounter(string.Format(MY_PLACES_COUNTER_TITLE, string.Empty));
                            break;
                    }
            }
            else
                view.SetPlacesGridLoadingMoreActive(true);

            if (!allFriendsLoaded)
            {
                List<Profile.CompactInfo> allFriends = await GetAllFriendsAsync(ct);
                placesStateService.SetAllFriends(allFriends);
                allFriendsLoaded = true;
            }

            if (!liveEventsLoaded)
            {
                List<EventDTO> liveEvents = await GetLiveEventsAsync(ct);
                placesStateService.SetLiveEvents(liveEvents);
                liveEventsLoaded = true;
            }

            Profile? ownProfile = await selfProfile.ProfileAsync(ct);
            if (ownProfile == null)
                return;

            Result<PlacesData.IPlacesAPIResponse> placesResult;

            switch (section)
            {
                case PlacesSection.BROWSE:
                    placesResult = await placesAPIService.SearchDestinationsAsync(
                                                              pageNumber: pageNumber, pageSize: PLACES_PER_PAGE, ct: ct,
                                                              searchText: currentFilters.SearchText,
                                                              sortBy: currentFilters.Section == PlacesSection.BROWSE ? currentFilters.SortBy : IPlacesAPIService.SortBy.NONE,
                                                              sortDirection: IPlacesAPIService.SortDirection.DESC,
                                                              category: !string.IsNullOrEmpty(currentFilters.SearchText) ? null : currentFilters.CategoryId,
                                                              withConnectedUsers: true,
                                                              onlySdk7: currentFilters.SDKVersion == IPlacesAPIService.SDKVersion.SDK7_ONLY,
                                                              withLiveEvents: true)
                                                         .SuppressToResultAsync(ReportCategory.PLACES);
                    break;
                case PlacesSection.FAVORITES:
                    placesResult = await placesAPIService.GetFavoritesDestinationsAsync(
                                                              ct: ct, pageNumber: pageNumber, pageSize: PLACES_PER_PAGE,
                                                              sortByBy: currentFilters.SortBy, sortDirection: IPlacesAPIService.SortDirection.DESC,
                                                              withConnectedUsers: true,
                                                              onlySdk7: false,
                                                              withLiveEvents: true)
                                                         .SuppressToResultAsync(ReportCategory.PLACES);
                    break;
                case PlacesSection.MY_PLACES:
                    placesResult = await placesAPIService.GetDestinationsByOwnerAsync(
                                                              ownerAddress: ownProfile.UserId,
                                                              ct: ct,
                                                              withConnectedUsers: true,
                                                              onlySdk7: false,
                                                              withLiveEvents: true)
                                                         .SuppressToResultAsync(ReportCategory.PLACES);
                    break;
                case PlacesSection.RECENTLY_VISITED:
                    var recentlyVisitedPlacesIds = placesAPIService.GetRecentlyVisitedPlaces();
                    var placesByIdResult = await placesAPIService.GetDestinationsByIdsAsync(recentlyVisitedPlacesIds, ct, withConnectedUsers: true)
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
                view.AddPlacesResultsItems(placesResult.Value.Data, pageNumber == 0, currentFilters.Section);
            }

            if (!string.IsNullOrEmpty(currentFilters.SearchText))
            {
                view.SetPlacesCounter(string.Format(SEARCH_COUNTER_TITLE, currentFilters.SearchText, $"({placesResult.Value.Total})"), showBackButton: true);
                PlacesSearched?.Invoke(currentFilters.SearchText, placesResult.Value.Total);
            }
            else
            {
                switch (currentFilters.Section)
                {
                    case PlacesSection.RECENTLY_VISITED:
                        view.SetPlacesCounter(string.Format(RECENT_VISITED_COUNTER_TITLE, $"({placesResult.Value.Total})"));
                        break;
                    case PlacesSection.FAVORITES:
                        view.SetPlacesCounter(string.Format(FAVORITES_COUNTER_TITLE, $"({placesResult.Value.Total})"));
                        break;
                    case PlacesSection.MY_PLACES:
                        view.SetPlacesCounter(string.Format(MY_PLACES_COUNTER_TITLE, $"({placesResult.Value.Total})"));
                        break;
                }

                PlacesFiltered?.Invoke(currentFilters);
            }

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
            view.ClearPlacesResults(null);
            placesStateService.ClearPlaces();
            placesStateService.ClearAllFriends();
            placesStateService.ClearLiveEvents();
            allFriendsLoaded = false;
            liveEventsLoaded = false;
        }

        private void OnPlaceSetAsHome(PlacesData.PlaceInfo placeInfo) =>
            view.RefreshOldPlaceAsHome(placeInfo.id);

        private async UniTask<List<Profile.CompactInfo>> GetAllFriendsAsync(CancellationToken ct)
        {
            var emptyResult = new List<Profile.CompactInfo>();

            if (!friendServiceProxy.Configured)
                return emptyResult;

            var result = await friendServiceProxy.StrictObject
                                                 .GetFriendsAsync(0, 1000, ct)
                                                 .SuppressToResultAsync(ReportCategory.PLACES);

            if (ct.IsCancellationRequested)
                return emptyResult;

            if (!result.Success)
            {
                NotificationsBusController.Instance.AddNotification(new ServerErrorNotification(GET_FRIENDS_ERROR_MESSAGE));
                return emptyResult;
            }

            return result.Value.Friends.ToList();
        }

        private async UniTask<List<EventDTO>> GetLiveEventsAsync(CancellationToken ct)
        {
            var emptyResult = new List<EventDTO>();

            Result<IReadOnlyList<EventDTO>> result = await eventsApiService.GetEventsAsync(ct, onlyLiveEvents: true)
                                                                                     .SuppressToResultAsync(ReportCategory.PLACES);

            if (ct.IsCancellationRequested)
                return emptyResult;

            if (!result.Success)
            {
                NotificationsBusController.Instance.AddNotification(new ServerErrorNotification(GET_LIVE_EVENTS_ERROR_MESSAGE));
                return emptyResult;
            }

            return result.Value.ToList();
        }
    }
}
