using Cysharp.Threading.Tasks;
using DCL.EventsApi;
using DCL.Optimization.Pools;
using DCL.PlacesAPIService;
using System;
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
        private readonly string searchText;
        private readonly NavmapSearchPlaceFilter filter;
        private readonly NavmapSearchPlaceSorting sorting;
        private readonly int pageNumber;
        private readonly int pageSize;
        private List<PlacesData.PlaceInfo>? places;
        private HashSet<string>? placesWithLiveEvents;

        public SearchForPlaceAndShowResultsCommand(
            IPlacesAPIService placesAPIService,
            IEventsApiService eventsApiService,
            PlacesAndEventsPanelController placesAndEventsPanelController,
            SearchResultPanelController searchResultPanelController,
            string searchText,
            NavmapSearchPlaceFilter filter,
            NavmapSearchPlaceSorting sorting,
            int pageNumber = 0,
            int pageSize = 8)
        {
            this.placesAPIService = placesAPIService;
            this.eventsApiService = eventsApiService;
            this.placesAndEventsPanelController = placesAndEventsPanelController;
            this.searchResultPanelController = searchResultPanelController;
            this.searchText = searchText;
            this.filter = filter;
            this.sorting = sorting;
            this.pageNumber = pageNumber;
            this.pageSize = pageSize;
        }

        public async UniTask ExecuteAsync(CancellationToken ct)
        {
            placesAndEventsPanelController.Toggle(PlacesAndEventsPanelController.Section.SEARCH);
            searchResultPanelController.SetLoadingState();

            await ProcessPlaces(ct);
            await ProcessLiveEvents(ct);
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

        private async UniTask ProcessLiveEvents(CancellationToken ct)
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

        private async UniTask ProcessPlaces(CancellationToken ct)
        {
            if (places == null)
            {
                places = ListPool<PlacesData.PlaceInfo>.Get();

                (IPlacesAPIService.SortBy sort, IPlacesAPIService.SortDirection sortDirection) = GetSorting();

                if (filter == NavmapSearchPlaceFilter.All)
                {
                    using PlacesData.IPlacesAPIResponse response = await placesAPIService.SearchPlacesAsync(searchText, pageNumber, pageSize, ct,
                        sort, sortDirection);
                    places.AddRange(response.Data);
                }
                else if (filter == NavmapSearchPlaceFilter.Favorites)
                {
                    using PoolExtensions.Scope<List<PlacesData.PlaceInfo>> response = await placesAPIService.GetFavoritesAsync(pageNumber, pageSize, ct,
                        // We have to renew cache, otherwise it throws an exception by trying to release the list from the pool
                        // Something is not right there
                        true, sort, sortDirection);
                    places.AddRange(response.Value);
                }
                else if (filter == NavmapSearchPlaceFilter.Visited)
                {
                    // TODO: implement visited places in local storage
                }
            }

            searchResultPanelController.SetResults(places!);
        }

        private (IPlacesAPIService.SortBy sort, IPlacesAPIService.SortDirection direction) GetSorting()
        {
            return sorting switch
                   {
                       NavmapSearchPlaceSorting.Newest => (IPlacesAPIService.SortBy.CREATED_AT, IPlacesAPIService.SortDirection.DESC),
                       NavmapSearchPlaceSorting.BestRated => (IPlacesAPIService.SortBy.LIKE_SCORE, IPlacesAPIService.SortDirection.DESC),
                       NavmapSearchPlaceSorting.MostActive => (IPlacesAPIService.SortBy.MOST_ACTIVE, IPlacesAPIService.SortDirection.DESC),
                       _ => throw new NotSupportedException()
                   };
        }
    }
}
