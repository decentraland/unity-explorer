using Cysharp.Threading.Tasks;
using DCL.Optimization.Pools;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace DCL.PlacesAPIService
{
    public interface IPlacesAPIService
    {
        [Obsolete("Use SearchDestinationsAsync instead")]
        UniTask<PlacesData.IPlacesAPIResponse> SearchPlacesAsync(int pageNumber, int pageSize, CancellationToken ct,
            string? searchText = null,
            SortBy sortBy = SortBy.MOST_ACTIVE, SortDirection sortDirection = SortDirection.DESC,
            string? category = null);

        UniTask<PlacesData.IPlacesAPIResponse> SearchDestinationsAsync(int pageNumber, int pageSize, CancellationToken ct,
            string? searchText = null,
            SortBy sortBy = SortBy.MOST_ACTIVE, SortDirection sortDirection = SortDirection.DESC,
            string? category = null,
            bool? withConnectedUsers = null,
            bool? onlySdk7 = null,
            bool? withLiveEvents = null,
            bool? onlyPlaces = null);

        /// <summary>
        ///     Every destination the Places menu tags as Featured, in the order the server ranks them.
        /// </summary>
        UniTask<PlacesData.IPlacesAPIResponse> GetHighlightedDestinationsAsync(CancellationToken ct);

        UniTask<PlacesData.PlaceInfo?> GetPlaceAsync(Vector2Int coords, CancellationToken ct, bool renewCache = false);

        UniTask<PlacesData.PlaceInfo?> GetWorldAsync(Vector2Int coords, string worldName, CancellationToken ct);

        [Obsolete("Use GetFavoritesDestinationsAsync instead")]
        UniTask<PlacesData.IPlacesAPIResponse> GetFavoritesAsync(CancellationToken ct,
            int pageNumber = -1, int pageSize = -1,
            SortBy sortByBy = SortBy.MOST_ACTIVE, SortDirection sortDirection = SortDirection.DESC);

        UniTask<PlacesData.IPlacesAPIResponse> GetFavoritesDestinationsAsync(CancellationToken ct,
            int pageNumber = -1, int pageSize = -1,
            SortBy sortByBy = SortBy.MOST_ACTIVE, SortDirection sortDirection = SortDirection.DESC,
            bool? withConnectedUsers = null,
            bool? onlySdk7 = null,
            bool? withLiveEvents = null,
            bool? onlyPlaces = null);

        UniTask<PoolExtensions.Scope<List<PlacesData.PlaceInfo>>> GetPlacesByCoordsListAsync(IEnumerable<Vector2Int> coordsList, CancellationToken ct, bool renewCache = false);
        [Obsolete("Use GetDestinationsByIdsAsync instead")]
        UniTask<PlacesData.IPlacesAPIResponse> GetPlacesByIdsAsync(IEnumerable<string> placeIds, CancellationToken ct, bool renewCache = false);
        UniTask<PlacesData.IPlacesAPIResponse> GetDestinationsByIdsAsync(IEnumerable<string> placeIds, CancellationToken ct, bool renewCache = false, bool? withConnectedUsers = null);
        [Obsolete("Use GetDestinationsByOwnerAsync instead")]
        UniTask<PlacesData.IPlacesAPIResponse> GetPlacesByOwnerAsync(string ownerAddress, CancellationToken ct, bool renewCache = false);
        UniTask<PlacesData.IPlacesAPIResponse> GetDestinationsByOwnerAsync(string ownerAddress, CancellationToken ct, bool renewCache = false, bool? withConnectedUsers = null, bool? onlySdk7 = null, bool? withLiveEvents = null);
        UniTask<PlacesData.IPlacesAPIResponse> GetWorldsByOwnerAsync(string ownerAddress, CancellationToken ct, bool renewCache = false);

        UniTask<IReadOnlyList<OptimizedPlaceInMapResponse>> GetOptimizedPlacesFromTheMapAsync(string category, CancellationToken ct);

        UniTask RatePlaceAsync(bool? isUpvote, string placeId, CancellationToken ct);

        UniTask SetPlaceFavoriteAsync(string placeId, bool isFavorite, CancellationToken ct);

        UniTask<IReadOnlyList<string>> GetPointsOfInterestCoordsAsync(CancellationToken ct, bool renewCache = false);

        UniTask ReportPlaceAsync(PlaceContentReportPayload placeContentReportPayload, CancellationToken ct);

        void ClearWorldsCache();

        void AddRecentlyVisitedPlace(string placeId);
        List<string> GetRecentlyVisitedPlaces();

        enum SortBy
        {
            NONE,
            MOST_ACTIVE,
            CREATED_AT,
            LIKE_SCORE,
        }

        enum SortDirection
        {
            DESC,
            ASC,
        }

        enum SDKVersion
        {
            SDK7_ONLY,
            ALL,
        }
    }

    public static class PlacesAPIServiceExtensions
    {
        public static async UniTask SetPlaceFavoriteAsync(this IPlacesAPIService placesAPIService, Vector2Int coords, bool isFavorite, CancellationToken ct)
        {
            PlacesData.PlaceInfo? place = await placesAPIService.GetPlaceAsync(coords, ct);
            if (place == null) return;
            await placesAPIService.SetPlaceFavoriteAsync(place.id, isFavorite, ct);
        }

        /// <summary>
        ///     Hydrates the recently visited history, most recent first. The destinations endpoint neither keeps the requested
        ///     order nor knows every visited place, so the result follows the history and skips the places it cannot resolve.
        /// </summary>
        public static async UniTask<PlacesData.IPlacesAPIResponse> GetRecentlyVisitedDestinationsAsync(this IPlacesAPIService placesAPIService, CancellationToken ct, bool? withConnectedUsers = null)
        {
            List<string> history = placesAPIService.GetRecentlyVisitedPlaces();
            var sorted = new PlacesData.PlacesAPIResponse { data = new List<PlacesData.PlaceInfo>(history.Count) };

            if (history.Count == 0)
                return sorted;

            PlacesData.IPlacesAPIResponse response = await placesAPIService.GetDestinationsByIdsAsync(history, ct, withConnectedUsers: withConnectedUsers);

            foreach (string placeId in history)
            {
                foreach (PlacesData.PlaceInfo place in response.Data)
                {
                    if (place.id != placeId) continue;

                    sorted.data.Add(place);
                    break;
                }
            }

            sorted.total = sorted.data.Count;
            return sorted;
        }
    }
}
