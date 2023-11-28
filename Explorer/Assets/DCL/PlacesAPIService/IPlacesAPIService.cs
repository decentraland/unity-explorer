using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace DCL.PlacesAPIService
{
    public interface IPlacesAPIService
    {
        UniTask<PlacesData.IPlacesAPIResponse> SearchPlacesAsync(string searchText, int pageNumber, int pageSize, CancellationToken ct);

        UniTask<(IReadOnlyList<PlacesData.PlaceInfo> places, int total)> GetMostActivePlacesAsync(int pageNumber, int pageSize, string filter = "", string sort = "", CancellationToken ct = default,
            bool renewCache = false);

        UniTask<PlacesData.PlaceInfo> GetPlaceAsync(Vector2Int coords, CancellationToken ct, bool renewCache = false);

        UniTask<PlacesData.PlaceInfo> GetPlaceAsync(string placeUUID, CancellationToken ct, bool renewCache = false);

        UniTask<IReadOnlyList<PlacesData.PlaceInfo>> GetFavoritesAsync(int pageNumber, int pageSize, CancellationToken ct, bool renewCache = false);

        UniTask<List<PlacesData.PlaceInfo>> GetPlacesByCoordsListAsync(IEnumerable<Vector2Int> coordsList, CancellationToken ct, bool renewCache = false);

        UniTask SetPlaceFavoriteAsync(string placeUUID, bool isFavorite, CancellationToken ct);

        UniTask SetPlaceVoteAsync(bool? isUpvote, string placeUUID, CancellationToken ct);

        UniTask SetPlaceFavoriteAsync(Vector2Int coords, bool isFavorite, CancellationToken ct);

        UniTask<bool> IsFavoritePlaceAsync(PlacesData.PlaceInfo placeInfo, CancellationToken ct, bool renewCache = false);

        UniTask<bool> IsFavoritePlaceAsync(Vector2Int coords, CancellationToken ct, bool renewCache = false);

        UniTask<bool> IsFavoritePlaceAsync(string placeUUID, CancellationToken ct, bool renewCache = false);

        UniTask<IReadOnlyList<string>> GetPointsOfInterestCoordsAsync(CancellationToken ct, bool renewCache = false);

        UniTask ReportPlaceAsync(PlaceContentReportPayload placeContentReportPayload, CancellationToken ct);
    }
}
