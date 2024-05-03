using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace DCL.PlacesAPIService
{
    public interface IPlacesAPIClient
    {
        /// <summary>
        ///     Search places by string
        /// </summary>
        /// <returns>Response is being pooled so it must be disposed after consumption</returns>
        UniTask<PlacesData.PlacesAPIResponse> SearchPlacesAsync(string searchString, int pageNumber, int pageSize, CancellationToken ct);

        /// <returns>Response is being pooled so it must be disposed after consumption</returns>
        UniTask<PlacesData.PlacesAPIResponse> GetMostActivePlacesAsync(int pageNumber, int pageSize, string filter = "", string sort = "", CancellationToken ct = default);

        /// <summary>
        ///     Calling GetPlace will allocate new list elements internally as it parses the full <see cref="PlacesData.PlacesAPIResponse" />
        ///     and the returns the first element of the list.
        /// </summary>
        /// <param name="coords"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        UniTask<PlacesData.PlaceInfo?> GetPlaceAsync(Vector2Int coords, CancellationToken ct);

        UniTask<PlacesData.PlaceInfo?> GetPlaceAsync(string placeUUID, CancellationToken ct);

        UniTask<PlacesData.IPlacesAPIResponse> GetFavoritesAsync(int pageNumber, int pageSize, CancellationToken ct);

        UniTask<PlacesData.IPlacesAPIResponse> GetAllFavoritesAsync(CancellationToken ct);

        UniTask<List<PlacesData.PlaceInfo>> GetPlacesByCoordsListAsync(IReadOnlyList<Vector2Int> coordsList, List<PlacesData.PlaceInfo> targetList, CancellationToken ct);

        UniTask ReportPlaceAsync(PlaceContentReportPayload placeContentReportPayload, CancellationToken ct);

        UniTask SetPlaceFavoriteAsync(string placeUUID, bool isFavorite, CancellationToken ct);

        UniTask SetPlaceVoteAsync(bool? isUpvote, string placeUUID, CancellationToken ct);

        UniTask<List<string>> GetPointsOfInterestCoordsAsync(CancellationToken ct);
    }
}
