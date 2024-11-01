using Cysharp.Threading.Tasks;
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
        private readonly SearchResultPanelController searchResultPanelController;
        private readonly string searchText;
        private readonly NavmapSearchPlaceFilter filter;
        private readonly NavmapSearchPlaceSorting sorting;
        private readonly int pageNumber;
        private readonly int pageSize;
        private List<PlacesData.PlaceInfo>? places;

        public SearchForPlaceAndShowResultsCommand(IPlacesAPIService placesAPIService,
            SearchResultPanelController searchResultPanelController,
            string searchText,
            NavmapSearchPlaceFilter filter,
            NavmapSearchPlaceSorting sorting,
            int pageNumber = 0,
            int pageSize = 8)
        {
            this.placesAPIService = placesAPIService;
            this.searchResultPanelController = searchResultPanelController;
            this.searchText = searchText;
            this.filter = filter;
            this.sorting = sorting;
            this.pageNumber = pageNumber;
            this.pageSize = pageSize;
        }

        public async UniTask ExecuteAsync(CancellationToken ct)
        {
            searchResultPanelController.Show();
            searchResultPanelController.SetLoadingState();

            if (places == null)
            {
                places = ListPool<PlacesData.PlaceInfo>.Get();

                (IPlacesAPIService.Sort sort, IPlacesAPIService.SortDirection sortDirection) = GetSorting();

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

        public void Undo()
        {
            searchResultPanelController.Hide();
            searchResultPanelController.ClearResults();
        }

        public void Dispose()
        {
            ListPool<PlacesData.PlaceInfo>.Release(places);
        }

        private (IPlacesAPIService.Sort sort, IPlacesAPIService.SortDirection direction) GetSorting()
        {
            return sorting switch
                   {
                       NavmapSearchPlaceSorting.Newest => (IPlacesAPIService.Sort.CREATED_AT, IPlacesAPIService.SortDirection.DESC),
                       NavmapSearchPlaceSorting.BestRated => (IPlacesAPIService.Sort.LIKE_SCORE, IPlacesAPIService.SortDirection.DESC),
                       NavmapSearchPlaceSorting.MostActive => (IPlacesAPIService.Sort.MOST_ACTIVE, IPlacesAPIService.SortDirection.DESC),
                       _ => throw new NotSupportedException()
                   };
        }
    }
}
