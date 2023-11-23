using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace DCL.PlacesAPIService
{
    public interface IPlacesAPIService
    {
        UniTask<PlacesData.IPlacesAPIResponse> SearchPlaces(string searchText, int pageNumber, int pageSize, CancellationToken ct);

        UniTask<(IReadOnlyList<PlacesData.PlaceInfo> places, int total)> GetMostActivePlaces(int pageNumber, int pageSize, string filter = "", string sort = "", CancellationToken ct = default,
            bool renewCache = false);

        UniTask<PlacesData.PlaceInfo> GetPlace(Vector2Int coords, CancellationToken ct, bool renewCache = false);

        UniTask<PlacesData.PlaceInfo> GetPlace(string placeUUID, CancellationToken ct, bool renewCache = false);

        UniTask<IReadOnlyList<PlacesData.PlaceInfo>> GetFavorites(int pageNumber, int pageSize, CancellationToken ct, bool renewCache = false);

        UniTask<List<PlacesData.PlaceInfo>> GetPlacesByCoordsList(IEnumerable<Vector2Int> coordsList, CancellationToken ct, bool renewCache = false);

        UniTask SetPlaceFavorite(string placeUUID, bool isFavorite, CancellationToken ct);

        UniTask SetPlaceVote(bool? isUpvote, string placeUUID, CancellationToken ct);

        UniTask SetPlaceFavorite(Vector2Int coords, bool isFavorite, CancellationToken ct);

        UniTask<bool> IsFavoritePlace(PlacesData.PlaceInfo placeInfo, CancellationToken ct, bool renewCache = false);

        UniTask<bool> IsFavoritePlace(Vector2Int coords, CancellationToken ct, bool renewCache = false);

        UniTask<bool> IsFavoritePlace(string placeUUID, CancellationToken ct, bool renewCache = false);

        UniTask<IReadOnlyList<string>> GetPointsOfInterestCoords(CancellationToken ct, bool renewCache = false);

        UniTask ReportPlace(PlaceContentReportPayload placeContentReportPayload, CancellationToken ct);
    }
}
