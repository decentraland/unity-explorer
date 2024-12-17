using Cysharp.Threading.Tasks;
using DCL.Optimization.Pools;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace DCL.PlacesAPIService
{
    public interface IPlacesAPIService
    {
        UniTask<PlacesData.IPlacesAPIResponse> SearchPlacesAsync(int pageNumber, int pageSize, CancellationToken ct,
            string? searchText = null,
            SortBy sortBy = SortBy.MOST_ACTIVE, SortDirection sortDirection = SortDirection.DESC,
            string? category = null);

        UniTask<PlacesData.PlaceInfo?> GetPlaceAsync(Vector2Int coords, CancellationToken ct, bool renewCache = false);

        UniTask<PlacesData.PlaceInfo?> GetPlaceAsync(string placeId, CancellationToken ct, bool renewCache = false);

        UniTask<PlacesData.IPlacesAPIResponse> GetFavoritesAsync(CancellationToken ct,
            int pageNumber = -1, int pageSize = -1,
            SortBy sortByBy = SortBy.MOST_ACTIVE, SortDirection sortDirection = SortDirection.DESC);

        UniTask<PoolExtensions.Scope<List<PlacesData.PlaceInfo>>> GetPlacesByCoordsListAsync(IEnumerable<Vector2Int> coordsList, CancellationToken ct, bool renewCache = false);

        UniTask<IReadOnlyList<OptimizedPlaceInMapResponse>> GetOptimizedPlacesFromTheMap(string category, CancellationToken ct);

        UniTask RatePlaceAsync(bool? isUpvote, string placeId, CancellationToken ct);

        UniTask SetPlaceFavoriteAsync(string placeId, bool isFavorite, CancellationToken ct);

        UniTask<IReadOnlyList<string>> GetPointsOfInterestCoordsAsync(CancellationToken ct, bool renewCache = false);

        UniTask ReportPlaceAsync(PlaceContentReportPayload placeContentReportPayload, CancellationToken ct);

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
    }

    public static class PlacesAPIServiceExtensions
    {
        public static async UniTask SetPlaceFavoriteAsync(this IPlacesAPIService placesAPIService, Vector2Int coords, bool isFavorite, CancellationToken ct)
        {
            PlacesData.PlaceInfo? place = await placesAPIService.GetPlaceAsync(coords, ct);
            if (place == null) return;
            await placesAPIService.SetPlaceFavoriteAsync(place.id, isFavorite, ct);
        }
    }
}
