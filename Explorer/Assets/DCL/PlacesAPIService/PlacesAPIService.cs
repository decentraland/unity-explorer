using Cysharp.Threading.Tasks;
using DCL.Optimization.Pools;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace DCL.PlacesAPIService
{
    public class PlacesAPIService : IPlacesAPIService
    {
        private static readonly ListObjectPool<string> COORDS_TO_REQ_POOL = new ();

        private readonly Dictionary<string, PlacesData.PlaceInfo> placesById = new ();
        private readonly Dictionary<Vector2Int, PlacesData.PlaceInfo> placesByCoords = new ();
        private readonly IPlacesAPIClient client;
        private readonly CancellationTokenSource disposeCts = new ();
        private readonly string[] singlePositionBuffer = new string[1];

        private UniTaskCompletionSource<PlacesData.IPlacesAPIResponse>? serverFavoritesCompletionSource;
        private List<string>? pointsOfInterestCoords;

        public PlacesAPIService(IPlacesAPIClient client)
        {
            this.client = client;
        }

        public async UniTask<PlacesData.IPlacesAPIResponse> SearchPlacesAsync(int pageNumber, int pageSize,
            CancellationToken ct,
            string? searchText = null,
            IPlacesAPIService.SortBy sortBy = IPlacesAPIService.SortBy.NONE,
            IPlacesAPIService.SortDirection sortDirection = IPlacesAPIService.SortDirection.DESC,
            string? category = null)
        {
            string sortByStr = string.Empty;
            string sortDirectionStr = string.Empty;

            if (sortBy != IPlacesAPIService.SortBy.NONE)
            {
                sortByStr = sortBy.ToString().ToLower();
                sortDirectionStr = sortDirection.ToString().ToLower();
            }

            return await client.GetPlacesAsync(ct,
                searchString: searchText, pagination: (pageNumber, pageSize),
                sortByStr, sortDirectionStr, category, addRealmDetails: true);
        }

        public async UniTask<PlacesData.PlaceInfo?> GetPlaceAsync(Vector2Int coords, CancellationToken ct, bool renewCache = false)
        {
            if (renewCache)
                placesByCoords.Remove(coords);
            else if (placesByCoords.TryGetValue(coords, out PlacesData.PlaceInfo placeInfo))
                return placeInfo;

            singlePositionBuffer[0] = $"{coords.x},{coords.y}";
            PlacesData.PlacesAPIResponse response = await client.GetPlacesAsync(ct, addRealmDetails: true,
                positions: singlePositionBuffer);

            if (!response.ok)
                return null;

            if (response.data.Count == 0)
                return null;

            PlacesData.PlaceInfo place = response.data[0];
            TryCachePlace(place);

            return place;
        }

        public async UniTask<PlacesData.PlaceInfo?> GetPlaceAsync(string placeId, CancellationToken ct, bool renewCache = false)
        {
            if (renewCache)
                placesById.Remove(placeId);
            else if (placesById.TryGetValue(placeId, out PlacesData.PlaceInfo placeInfo))
                return placeInfo;

            PlacesData.PlaceInfo? place = await client.GetPlaceAsync(placeId, ct);
            TryCachePlace(place);
            return place;
        }

        public async UniTask<PoolExtensions.Scope<List<PlacesData.PlaceInfo>>> GetPlacesByCoordsListAsync(
            IEnumerable<Vector2Int> coordsList, CancellationToken ct, bool renewCache = false)
        {
            using PoolExtensions.Scope<List<PlacesData.PlaceInfo>> rentedAlreadyCachedPlaces = PlacesData.PLACE_INFO_LIST_POOL.AutoScope();
            using PoolExtensions.Scope<List<string>> coordsToRequest = COORDS_TO_REQ_POOL.AutoScope();

            List<PlacesData.PlaceInfo> alreadyCachedPlaces = rentedAlreadyCachedPlaces.Value;

            foreach (Vector2Int coords in coordsList)
            {
                if (renewCache)
                {
                    placesByCoords.Remove(coords);
                    coordsToRequest.Value.Add($"{coords.x},{coords.y}");
                }
                else
                {
                    if (placesByCoords.TryGetValue(coords, out PlacesData.PlaceInfo placeInfo))
                        alreadyCachedPlaces.Add(placeInfo);
                    else
                        coordsToRequest.Value.Add($"{coords.x},{coords.y}");
                }
            }

            PoolExtensions.Scope<List<PlacesData.PlaceInfo>> rentedPlaces = PlacesData.PLACE_INFO_LIST_POOL.AutoScope();
            List<PlacesData.PlaceInfo> places = rentedPlaces.Value;

            if (coordsToRequest.Value.Count > 0)
            {
                await client.GetPlacesAsync(ct, positions: coordsToRequest.Value, resultBuffer: places, addRealmDetails: true);

                foreach (PlacesData.PlaceInfo place in places)
                    TryCachePlace(place);
            }

            places.AddRange(alreadyCachedPlaces);
            return rentedPlaces;
        }

        public async UniTask<IReadOnlyList<OptimizedPlaceInMapResponse>> GetOptimizedPlacesFromTheMap(string category, CancellationToken ct) =>
            await client.GetOptimizedPlacesFromTheMap(category, ct);

        public async UniTask<PlacesData.IPlacesAPIResponse> GetFavoritesAsync(CancellationToken ct,
            int pageNumber = -1, int pageSize = -1,
            IPlacesAPIService.SortBy sortBy = IPlacesAPIService.SortBy.MOST_ACTIVE,
            IPlacesAPIService.SortDirection sortDirection = IPlacesAPIService.SortDirection.DESC)
        {
            string sortByStr = string.Empty;
            string sortDirectionStr = string.Empty;

            if (sortBy != IPlacesAPIService.SortBy.NONE)
            {
                sortByStr = sortBy.ToString().ToLower();
                sortDirectionStr = sortDirection.ToString().ToLower();
            }

            return await client.GetPlacesAsync(ct,
                pagination: pageNumber == -1 && pageSize == -1 ? null : (pageNumber, pageSize),
                sortBy: sortByStr, sortDirection: sortDirectionStr, addRealmDetails: true,
                onlyFavorites: true);
        }

        public async UniTask SetPlaceFavoriteAsync(string placeId, bool isFavorite, CancellationToken ct) =>
            await client.SetPlaceFavoriteAsync(placeId, isFavorite, ct);

        public async UniTask RatePlaceAsync(bool? isUpvote, string placeId, CancellationToken ct)
        {
            await client.RatePlaceAsync(isUpvote, placeId, ct);
        }

        public async UniTask<IReadOnlyList<string>> GetPointsOfInterestCoordsAsync(CancellationToken ct, bool renewCache = false)
        {
            if (renewCache || pointsOfInterestCoords == null)
                pointsOfInterestCoords = await client.GetPointsOfInterestCoordsAsync(ct);

            return pointsOfInterestCoords;
        }

        public async UniTask ReportPlaceAsync(PlaceContentReportPayload placeContentReportPayload, CancellationToken ct) =>
            await client.ReportPlaceAsync(placeContentReportPayload, ct);

        public void Dispose()
        {
            disposeCts.Cancel();
            disposeCts.Dispose();
        }

        private void TryCachePlace(PlacesData.PlaceInfo? placeInfo)
        {
            if (placeInfo == null)
                return;

            placesById[placeInfo.id] = placeInfo;

            foreach (Vector2Int placeInfoPosition in placeInfo.Positions)
                placesByCoords[placeInfoPosition] = placeInfo;
        }
    }
}
