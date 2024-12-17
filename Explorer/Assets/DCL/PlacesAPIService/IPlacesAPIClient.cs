using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;

namespace DCL.PlacesAPIService
{
    public interface IPlacesAPIClient
    {
        /// <summary>
        /// Search places by several parameters
        /// </summary>
        /// <returns>Response is being pooled so it must be disposed after consumption</returns>
        UniTask<PlacesData.PlacesAPIResponse> GetPlacesAsync(CancellationToken ct,
            string? searchString = null,
            (int pageNumber, int pageSize)? pagination = null,
            string? sortBy = null, string? sortDirection = null,
            string? category = null,
            bool? onlyFavorites = null,
            bool? addRealmDetails = null,
            IReadOnlyList<string>? positions = null,
            List<PlacesData.PlaceInfo>? resultBuffer = null);

        UniTask<PlacesData.PlaceInfo?> GetPlaceAsync(string placeId, CancellationToken ct);

        UniTask ReportPlaceAsync(PlaceContentReportPayload placeContentReportPayload, CancellationToken ct);

        UniTask SetPlaceFavoriteAsync(string placeId, bool isFavorite, CancellationToken ct);

        UniTask RatePlaceAsync(bool? isUpvote, string placeId, CancellationToken ct);

        UniTask<List<string>> GetPointsOfInterestCoordsAsync(CancellationToken ct);

        /// <summary>
        /// Removes the excess of data from the places/map api, so it can be represented in the map with the minimal information
        /// </summary>
        UniTask<IReadOnlyList<OptimizedPlaceInMapResponse>> GetOptimizedPlacesFromTheMap(string category, CancellationToken ct);
    }
}
