using Cysharp.Threading.Tasks;
using DCL.EventsApi;
using DCL.PlacesAPIService;
using System.Collections.Generic;
using System.Threading;
using UnityEngine.Pool;

namespace DCL.Navmap
{
    public class SearchForPlaceAndShowResultsCommand : INavmapCommand
    {
        private readonly IPlacesAPIService placesAPIService;
        private readonly IEventsApiService eventsApiService;
        private readonly PlacesAndEventsPanelController placesAndEventsPanelController;
        private readonly SearchResultPanelController searchResultPanelController;
        private readonly NavmapSearchBarController searchBarController;
        private readonly INavmapBus.SearchPlaceResultDelegate callback;
        private readonly INavmapBus.SearchPlaceParams @params;
        private List<PlacesData.PlaceInfo>? places;
        private HashSet<string>? placesWithLiveEvents;
        private int totalResultCount;

        public SearchForPlaceAndShowResultsCommand(
            IPlacesAPIService placesAPIService,
            IEventsApiService eventsApiService,
            PlacesAndEventsPanelController placesAndEventsPanelController,
            SearchResultPanelController searchResultPanelController,
            NavmapSearchBarController searchBarController,
            INavmapBus.SearchPlaceResultDelegate callback,
            INavmapBus.SearchPlaceParams @params)
        {
            this.placesAPIService = placesAPIService;
            this.eventsApiService = eventsApiService;
            this.placesAndEventsPanelController = placesAndEventsPanelController;
            this.searchResultPanelController = searchResultPanelController;
            this.searchBarController = searchBarController;
            this.callback = callback;
            this.@params = @params;
        }

        public async UniTask ExecuteAsync(CancellationToken ct)
        {
            placesAndEventsPanelController.Toggle(PlacesAndEventsPanelController.Section.SEARCH);
            searchResultPanelController.ClearResults();
            searchResultPanelController.SetLoadingState();

            await ProcessPlacesAsync(ct);
            await ProcessLiveEventsAsync(ct);

            callback.Invoke(@params, places!, totalResultCount);
        }

        public void Undo()
        {
            searchResultPanelController.Hide();
            searchResultPanelController.ClearResults();
        }

        public void Dispose()
        {
            ListPool<PlacesData.PlaceInfo>.Release(places);
            HashSetPool<string>.Release(placesWithLiveEvents);
            places = null;
            placesWithLiveEvents = null;
        }

        private async UniTask ProcessLiveEventsAsync(CancellationToken ct)
        {
            if (places == null) return;

            if (placesWithLiveEvents == null)
            {
                placesWithLiveEvents = HashSetPool<string>.Get();
                HashSet<string> placesIds = HashSetPool<string>.Get();

                foreach (PlacesData.PlaceInfo place in places)
                    placesIds.Add(place.base_position);

                try
                {
                    IReadOnlyList<EventDTO> eventsForCurrentPlaces = await eventsApiService.GetEventsByParcelAsync(placesIds, ct,
                        true);

                    foreach (EventDTO @event in eventsForCurrentPlaces)
                        if (@event.live)
                            placesWithLiveEvents.Add($"{@event.x},${@event.y}");
                }
                finally
                {
                    HashSetPool<string>.Release(placesIds);
                }
            }

            searchResultPanelController.SetLiveEvents(placesWithLiveEvents);
        }

        private async UniTask ProcessPlacesAsync(CancellationToken ct)
        {
            searchBarController.SetInputText(@params.text ?? @params.category ?? string.Empty);
            searchBarController.Interactable = true;

            if (places == null)
            {
                places = ListPool<PlacesData.PlaceInfo>.Get();

                (IPlacesAPIService.SortBy sort, IPlacesAPIService.SortDirection sortDirection) = GetSorting();

                if (@params.filter == NavmapSearchPlaceFilter.All)
                {
                    using PlacesData.IPlacesAPIResponse response = await placesAPIService.SearchPlacesAsync(@params.page, @params.pageSize, ct,
                        searchText: @params.text,
                        sortBy: sort, sortDirection: sortDirection,
                        category: @params.category);
                    places.AddRange(response.Data);
                    totalResultCount = response.Total;
                }
                else if (@params.filter == NavmapSearchPlaceFilter.Favorites)
                {
                    using PlacesData.IPlacesAPIResponse response = await placesAPIService.GetFavoritesAsync(
                        ct,
                        pageNumber: @params.page, pageSize: @params.pageSize,
                        sortByBy: sort, sortDirection: sortDirection);
                    places.AddRange(response.Data);
                    totalResultCount = response.Total;
                }
                else if (@params.filter == NavmapSearchPlaceFilter.Visited)
                {
                    // TODO: implement visited places
                }
            }

            searchResultPanelController.SetResults(places!);
            searchResultPanelController.SetPagination(@params.page, @params.pageSize, totalResultCount, @params);
        }

        private (IPlacesAPIService.SortBy sort, IPlacesAPIService.SortDirection direction) GetSorting()
        {
            return @params.sorting switch
                   {
                       NavmapSearchPlaceSorting.Newest => (IPlacesAPIService.SortBy.CREATED_AT, IPlacesAPIService.SortDirection.DESC),
                       NavmapSearchPlaceSorting.BestRated => (IPlacesAPIService.SortBy.LIKE_SCORE, IPlacesAPIService.SortDirection.DESC),
                       NavmapSearchPlaceSorting.MostActive => (IPlacesAPIService.SortBy.MOST_ACTIVE, IPlacesAPIService.SortDirection.DESC),
                       _ => (IPlacesAPIService.SortBy.NONE, IPlacesAPIService.SortDirection.DESC),
                   };
        }
    }
}
