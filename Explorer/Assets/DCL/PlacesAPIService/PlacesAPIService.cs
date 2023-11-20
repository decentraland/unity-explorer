using Cysharp.Threading.Tasks;
using DCLServices.Lambdas;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace DCLServices.PlacesAPIService
{
    public interface IPlacesAPIService
    {
        UniTask<(IReadOnlyList<PlacesData.PlaceInfo> places, int total)> SearchPlaces(string searchText, int pageNumber, int pageSize, CancellationToken ct, bool renewCache = false);

        UniTask<(IReadOnlyList<PlacesData.PlaceInfo> places, int total)> GetMostActivePlaces(int pageNumber, int pageSize, string filter = "", string sort = "", CancellationToken ct = default, bool renewCache = false);

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

    public class PlacesAPIService : IPlacesAPIService, ILambdaServiceConsumer<PlacesData.PlacesAPIResponse>
    {
        private readonly IPlacesAPIClient client;

        internal readonly Dictionary<string, LambdaResponsePagePointer<PlacesData.PlacesAPIResponse>> activePlacesPagePointers = new ();
        internal readonly Dictionary<string, PlacesData.PlaceInfo> placesById = new ();
        internal readonly Dictionary<Vector2Int, PlacesData.PlaceInfo> placesByCoords = new ();
        private List<string> pointsOfInterestCoords;

        //Favorites
        internal bool composedFavoritesDirty = true;
        internal readonly List<PlacesData.PlaceInfo> composedFavorites = new ();
        internal UniTaskCompletionSource<List<PlacesData.PlaceInfo>> serverFavoritesCompletionSource = null;
        private DateTime serverFavoritesLastRetrieval = DateTime.MinValue;
        internal readonly Dictionary<string, bool> localFavorites = new ();

        private readonly CancellationTokenSource disposeCts = new ();

        public PlacesAPIService(IPlacesAPIClient client)
        {
            this.client = client;
        }
        public void Initialize() { }

        public async UniTask<(IReadOnlyList<PlacesData.PlaceInfo> places, int total)> SearchPlaces(string searchText, int pageNumber, int pageSize, CancellationToken ct, bool renewCache = false)
        {
            PlacesData.PlacesAPIResponse placesAPIResponse = await client.SearchPlaces(searchText, pageNumber, pageSize, ct);
            return (placesAPIResponse.data, placesAPIResponse.total);
        }

        public async UniTask<(IReadOnlyList<PlacesData.PlaceInfo> places, int total)> GetMostActivePlaces(int pageNumber, int pageSize, string filter = "", string sort = "", CancellationToken ct = default, bool renewCache = false)
        {
            var createNewPointer = false;

            if (!activePlacesPagePointers.TryGetValue($"{pageSize}_{filter}_{sort}", out var pagePointer)) { createNewPointer = true; }
            else if (renewCache)
            {
                pagePointer.Dispose();
                activePlacesPagePointers.Remove($"{pageSize}_{filter}_{sort}");
                createNewPointer = true;
            }

            if (createNewPointer)
            {
                activePlacesPagePointers[$"{pageSize}_{filter}_{sort}"] = pagePointer = new LambdaResponsePagePointer<PlacesData.PlacesAPIResponse>(
                    $"", // not needed, the consumer will compose the URL
                    pageSize, disposeCts.Token, this, TimeSpan.FromSeconds(30));
            }

            (PlacesData.PlacesAPIResponse response, bool _) = await pagePointer.GetPageAsync(pageNumber, ct, new Dictionary<string, string>(){{"filter", filter},{"sort", sort}});

            foreach (PlacesData.PlaceInfo place in response.data)
            {
                CachePlace(place);
            }

            return (response.data, response.total);
        }

        public async UniTask<PlacesData.PlaceInfo> GetPlace(Vector2Int coords, CancellationToken ct, bool renewCache = false)
        {
            if (renewCache)
                placesByCoords.Remove(coords);
            else if (placesByCoords.TryGetValue(coords, out var placeInfo))
                return placeInfo;

            var place = await client.GetPlace(coords, ct);
            CachePlace(place);
            return place;
        }

        public async UniTask<PlacesData.PlaceInfo> GetPlace(string placeUUID, CancellationToken ct, bool renewCache = false)
        {
            if (renewCache)
                placesById.Remove(placeUUID);
            else if (placesById.TryGetValue(placeUUID, out var placeInfo))
                return placeInfo;

            var place = await client.GetPlace(placeUUID, ct);
            CachePlace(place);
            return place;
        }

        public async UniTask<List<PlacesData.PlaceInfo>> GetPlacesByCoordsList(IEnumerable<Vector2Int> coordsList, CancellationToken ct, bool renewCache = false)
        {
            List<PlacesData.PlaceInfo> alreadyCachedPlaces = new ();
            List<Vector2Int> coordsToRequest = new ();

            foreach (Vector2Int coords in coordsList)
            {
                if (renewCache)
                {
                    placesByCoords.Remove(coords);
                    coordsToRequest.Add(coords);
                }
                else
                {
                    if (placesByCoords.TryGetValue(coords, out var placeInfo))
                        alreadyCachedPlaces.Add(placeInfo);
                    else
                        coordsToRequest.Add(coords);
                }
            }

            var places = new List<PlacesData.PlaceInfo>();
            if (coordsToRequest.Count > 0)
            {
                places = await client.GetPlacesByCoordsList(coordsToRequest, ct);
                foreach (var place in places)
                    CachePlace(place);
            }

            places.AddRange(alreadyCachedPlaces);

            return places;
        }

        public async UniTask<IReadOnlyList<PlacesData.PlaceInfo>> GetFavorites(int pageNumber, int pageSize, CancellationToken ct, bool renewCache = false)
        {
            const int CACHE_EXPIRATION = 30; // Seconds

            // We need to pass the source to avoid conflicts with parallel calls forcing renewCache
            async UniTask RetrieveFavorites(UniTaskCompletionSource<List<PlacesData.PlaceInfo>> source)
            {
                List<PlacesData.PlaceInfo> favorites;
                // We dont use the ct param, otherwise the whole flow would be cancel if the first call is cancelled
                if (pageNumber == -1 && pageSize == -1)
                {
                    favorites = await client.GetAllFavorites(ct);
                }
                else
                {
                    favorites = await client.GetFavorites(pageNumber, pageSize, disposeCts.Token);
                }
                foreach (PlacesData.PlaceInfo place in favorites)
                {
                    CachePlace(place);
                }
                composedFavoritesDirty = true;
                source.TrySetResult(favorites);
            }

            if (serverFavoritesCompletionSource == null || renewCache || (DateTime.Now - serverFavoritesLastRetrieval) > TimeSpan.FromSeconds(CACHE_EXPIRATION))
            {
                localFavorites.Clear();
                serverFavoritesLastRetrieval = DateTime.Now;
                serverFavoritesCompletionSource = new UniTaskCompletionSource<List<PlacesData.PlaceInfo>>();
                RetrieveFavorites(serverFavoritesCompletionSource).Forget();
            }

            List<PlacesData.PlaceInfo> serverFavorites = await serverFavoritesCompletionSource.Task.AttachExternalCancellation(ct);

            if (!composedFavoritesDirty)
                return composedFavorites;

            composedFavorites.Clear();
            foreach (PlacesData.PlaceInfo serverFavorite in serverFavorites)
            {
                //skip if it's already in the local favorites cache, it will be added (or not) later
                if(localFavorites.ContainsKey(serverFavorite.id))
                    continue;
                composedFavorites.Add(serverFavorite);
            }

            foreach ((string placeUUID, bool isFavorite) in localFavorites)
            {
                if (!isFavorite)
                    continue;

                if(placesById.TryGetValue(placeUUID, out var place))
                    composedFavorites.Add(place);
            }
            composedFavoritesDirty = false;

            return composedFavorites;
        }

        public async UniTask SetPlaceFavorite(string placeUUID, bool isFavorite, CancellationToken ct)
        {
            localFavorites[placeUUID] = isFavorite;
            composedFavoritesDirty = true;
            await client.SetPlaceFavorite(placeUUID, isFavorite, ct);
        }

        public async UniTask SetPlaceVote(bool? isUpvote, string placeUUID, CancellationToken ct)
        {
            await client.SetPlaceVote(isUpvote, placeUUID, ct);
        }

        public async UniTask SetPlaceFavorite(Vector2Int coords, bool isFavorite, CancellationToken ct)
        {
            var place = await GetPlace(coords, ct);
            await SetPlaceFavorite(place.id, isFavorite, ct);
        }

        public async UniTask<bool> IsFavoritePlace(PlacesData.PlaceInfo placeInfo, CancellationToken ct, bool renewCache = false)
        {
            var favorites = await GetFavorites(-1,-1, ct, renewCache);

            foreach (PlacesData.PlaceInfo favorite in favorites)
            {
                if (favorite.id == placeInfo.id)
                    return true;
            }

            return false;
        }

        public async UniTask<bool> IsFavoritePlace(Vector2Int coords, CancellationToken ct, bool renewCache = false)
        {
            // We could call IsFavoritePlace with the placeInfo and avoid code repetition, but this way we can have the calls in parallel
            (PlacesData.PlaceInfo placeInfo, IReadOnlyList<PlacesData.PlaceInfo> favorites) = await UniTask.WhenAll(GetPlace(coords, ct, renewCache), GetFavorites(0,1000, ct, renewCache));

            foreach (PlacesData.PlaceInfo favorite in favorites)
            {
                if (favorite.id == placeInfo.id)
                    return true;
            }

            return false;
        }

        public async UniTask<bool> IsFavoritePlace(string placeUUID, CancellationToken ct, bool renewCache = false)
        {
            // We could call IsFavoritePlace with the placeInfo and avoid code repetition, but this way we can have the calls in parallel
            (PlacesData.PlaceInfo placeInfo, IReadOnlyList<PlacesData.PlaceInfo> favorites) = await UniTask.WhenAll(GetPlace(placeUUID, ct, renewCache), GetFavorites( 0, 1000, ct, renewCache));

            foreach (PlacesData.PlaceInfo favorite in favorites)
            {
                if (favorite.id == placeInfo.id)
                    return true;
            }

            return false;
        }

        internal void CachePlace(PlacesData.PlaceInfo placeInfo)
        {
            placesById[placeInfo.id] = placeInfo;

            foreach (Vector2Int placeInfoPosition in placeInfo.Positions) { placesByCoords[placeInfoPosition] = placeInfo; }
        }

        public async UniTask<(PlacesData.PlacesAPIResponse response, bool success)> CreateRequest(string endPoint, int pageSize, int pageNumber, Dictionary<string,string> additionalData, CancellationToken ct = default)
        {
            var response = await client.GetMostActivePlaces(pageNumber, pageSize,additionalData["filter"],additionalData["sort"], ct);
            // Client will handle most of the error handling and throw if needed
            return (response, true);
        }

        public async UniTask<IReadOnlyList<string>> GetPointsOfInterestCoords(CancellationToken ct, bool renewCache = false)
        {
            if (renewCache || pointsOfInterestCoords == null)
                pointsOfInterestCoords = await client.GetPointsOfInterestCoords(ct);

            return pointsOfInterestCoords;
        }

        public async UniTask ReportPlace(PlaceContentReportPayload placeContentReportPayload, CancellationToken ct) =>
            await client.ReportPlace(placeContentReportPayload, ct);

        public void Dispose()
        {
            disposeCts.Cancel();
            disposeCts.Dispose();
        }
    }
}
