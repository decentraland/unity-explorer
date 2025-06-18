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
            List<PlacesData.PlaceInfo>? resultBuffer = null,
            string? ownerAddress = null);

        UniTask<PlacesData.PlacesAPIResponse> GetWorldsAsync(CancellationToken ct,
            string? searchString = null,
            (int pageNumber, int pageSize)? pagination = null,
            string? sortBy = null, string? sortDirection = null,
            string? category = null,
            bool? onlyFavorites = null,
            IReadOnlyList<string>? names = null,
            List<PlacesData.PlaceInfo>? resultBuffer = null,
            bool? showDisabled = null,
            string? ownerAddress = null);

        UniTask<PlacesData.PlacesAPIResponse> GetWorldAsync(string placeId, CancellationToken ct);
        UniTask<PlacesData.PlacesAPIResponse> GetPlacesByIdsAsync(IEnumerable<string> placeIds, CancellationToken ct);

        UniTask ReportPlaceAsync(PlaceContentReportPayload placeContentReportPayload, CancellationToken ct);

        UniTask SetPlaceFavoriteAsync(string placeId, bool isFavorite, CancellationToken ct);

        UniTask RatePlaceAsync(bool? isUpvote, string placeId, CancellationToken ct);

        UniTask<List<string>> GetPointsOfInterestCoordsAsync(CancellationToken ct);

        /// <summary>
        /// Removes the excess of data from the places/map api, so it can be represented in the map with the minimal information
        /// </summary>
        UniTask<IReadOnlyList<OptimizedPlaceInMapResponse>> GetOptimizedPlacesFromTheMapAsync(string category, CancellationToken ct);
    }
}
