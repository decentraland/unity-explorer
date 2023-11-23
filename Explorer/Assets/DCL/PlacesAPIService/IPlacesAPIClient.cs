using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace DCL.PlacesAPIService
{
    public interface IPlacesAPIClient
    {
        UniTask<PlacesData.PlacesAPIResponse> SearchPlaces(string searchString, int pageNumber, int pageSize, CancellationToken ct);

        UniTask<PlacesData.PlacesAPIResponse> GetMostActivePlaces(int pageNumber, int pageSize, string filter = "", string sort = "", CancellationToken ct = default);

        UniTask<PlacesData.PlaceInfo> GetPlace(Vector2Int coords, CancellationToken ct);

        UniTask<PlacesData.PlaceInfo> GetPlace(string placeUUID, CancellationToken ct);

        UniTask<List<PlacesData.PlaceInfo>> GetFavorites(int pageNumber, int pageSize, CancellationToken ct);

        UniTask<List<PlacesData.PlaceInfo>> GetAllFavorites(CancellationToken ct);

        UniTask<List<PlacesData.PlaceInfo>> GetPlacesByCoordsList(List<Vector2Int> coordsList, CancellationToken ct);

        UniTask ReportPlace(PlaceContentReportPayload placeContentReportPayload, CancellationToken ct);

        UniTask SetPlaceFavorite(string placeUUID, bool isFavorite, CancellationToken ct);

        UniTask SetPlaceVote(bool? isUpvote, string placeUUID, CancellationToken ct);

        UniTask<List<string>> GetPointsOfInterestCoords(CancellationToken ct);
    }
}
