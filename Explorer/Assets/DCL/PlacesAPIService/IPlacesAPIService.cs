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

        UniTask<PoolExtensions.Scope<List<PlacesData.PlaceInfo>>> GetFavoritesAsync(int pageNumber, int pageSize, CancellationToken ct, bool renewCache = false,
            SortBy sortByBy = SortBy.MOST_ACTIVE, SortDirection sortDirection = SortDirection.DESC,
            string? category = null);

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
        public static async UniTask<bool> IsFavoritePlaceAsync(this IPlacesAPIService placesAPIService, PlacesData.PlaceInfo placeInfo, CancellationToken ct, bool renewCache = false)
        {
            using PoolExtensions.Scope<List<PlacesData.PlaceInfo>> favorites = await placesAPIService.GetFavoritesAsync(-1, -1, ct, renewCache);

            foreach (PlacesData.PlaceInfo favorite in favorites.Value)
                if (favorite.id == placeInfo.id)
                    return true;

            return false;
        }

        public static async UniTask<bool> IsFavoritePlaceAsync(this IPlacesAPIService placesAPIService, Vector2Int coords, CancellationToken ct, bool renewCache = false)
        {
            (PlacesData.PlaceInfo? placeInfo, PoolExtensions.Scope<List<PlacesData.PlaceInfo>> favorites) = await UniTask.WhenAll(
                placesAPIService.GetPlaceAsync(coords, ct, renewCache),
                placesAPIService.GetFavoritesAsync(0, 1000, ct, renewCache)
            );

            foreach (PlacesData.PlaceInfo favorite in favorites.Value)
                if (favorite.id == placeInfo?.id)
                    return true;

            return false;
        }

        public static async UniTask<bool> IsFavoritePlaceAsync(this IPlacesAPIService placesAPIService, string placeUUID, CancellationToken ct, bool renewCache = false)
        {
            (PlacesData.PlaceInfo? placeInfo, PoolExtensions.Scope<List<PlacesData.PlaceInfo>> favorites) = await UniTask.WhenAll(
                placesAPIService.GetPlaceAsync(placeUUID, ct, renewCache),
                placesAPIService.GetFavoritesAsync(0, 1000, ct, renewCache)
            );

            foreach (PlacesData.PlaceInfo favorite in favorites.Value)
                if (favorite.id == placeInfo?.id)
                    return true;

            return false;
        }

        public static async UniTask SetPlaceFavoriteAsync(this IPlacesAPIService placesAPIService, Vector2Int coords, bool isFavorite, CancellationToken ct)
        {
            PlacesData.PlaceInfo? place = await placesAPIService.GetPlaceAsync(coords, ct);
            await placesAPIService.SetPlaceFavoriteAsync(place.id!, isFavorite, ct);
        }
    }
}
