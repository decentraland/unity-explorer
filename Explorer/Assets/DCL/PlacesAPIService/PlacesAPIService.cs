using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Utility.Pool;

namespace DCL.PlacesAPIService
{
    public partial class PlacesAPIService : IPlacesAPIService
    {
        private static readonly ListObjectPool<Vector2Int> COORDS_TO_REQ_POOL = new ();

        internal readonly Dictionary<string, PlacesData.PlaceInfo> placesById = new ();
        internal readonly Dictionary<Vector2Int, PlacesData.PlaceInfo> placesByCoords = new ();
        internal readonly List<PlacesData.PlaceInfo> composedFavorites = new ();
        internal readonly Dictionary<string, bool> localFavorites = new ();

        private readonly IPlacesAPIClient client;

        private readonly CancellationTokenSource disposeCts = new ();

        //Favorites
        internal bool composedFavoritesDirty = true;
        internal UniTaskCompletionSource<PlacesData.IPlacesAPIResponse> serverFavoritesCompletionSource;
        private List<string> pointsOfInterestCoords;
        private DateTime serverFavoritesLastRetrieval = DateTime.MinValue;

        public PlacesAPIService(IPlacesAPIClient client)
        {
            this.client = client;
        }

        public async UniTask<PlacesData.IPlacesAPIResponse> SearchPlaces(string searchText, int pageNumber, int pageSize, CancellationToken ct) =>
            await client.SearchPlaces(searchText, pageNumber, pageSize, ct);

        public async UniTask<PlacesData.PlaceInfo> GetPlace(Vector2Int coords, CancellationToken ct, bool renewCache = false)
        {
            if (renewCache)
                placesByCoords.Remove(coords);
            else if (placesByCoords.TryGetValue(coords, out PlacesData.PlaceInfo placeInfo))
                return placeInfo;

            PlacesData.PlaceInfo place = await client.GetPlace(coords, ct);
            CachePlace(place);
            return place;
        }

        public async UniTask<PlacesData.PlaceInfo> GetPlace(string placeUUID, CancellationToken ct, bool renewCache = false)
        {
            if (renewCache)
                placesById.Remove(placeUUID);
            else if (placesById.TryGetValue(placeUUID, out PlacesData.PlaceInfo placeInfo))
                return placeInfo;

            PlacesData.PlaceInfo place = await client.GetPlace(placeUUID, ct);
            CachePlace(place);
            return place;
        }

        public async UniTask<List<PlacesData.PlaceInfo>> GetPlacesByCoordsList(IEnumerable<Vector2Int> coordsList, CancellationToken ct, bool renewCache = false)
        {
            using PoolExtensions.Scope<List<PlacesData.PlaceInfo>> rentedAlreadyCachedPlaces = PlacesData.PLACE_INFO_LIST_POOL.AutoScope();
            using PoolExtensions.Scope<List<Vector2Int>> coordsToRequest = COORDS_TO_REQ_POOL.AutoScope();

            List<PlacesData.PlaceInfo> alreadyCachedPlaces = rentedAlreadyCachedPlaces.Value;

            foreach (Vector2Int coords in coordsList)
            {
                if (renewCache)
                {
                    placesByCoords.Remove(coords);
                    coordsToRequest.Value.Add(coords);
                }
                else
                {
                    if (placesByCoords.TryGetValue(coords, out PlacesData.PlaceInfo placeInfo))
                        alreadyCachedPlaces.Add(placeInfo);
                    else
                        coordsToRequest.Value.Add(coords);
                }
            }

            using PoolExtensions.Scope<List<PlacesData.PlaceInfo>> rentedPlaces = PlacesData.PLACE_INFO_LIST_POOL.AutoScope();
            List<PlacesData.PlaceInfo> places = rentedPlaces.Value;

            if (coordsToRequest.Value.Count > 0)
            {
                places = await client.GetPlacesByCoordsList(coordsToRequest.Value, places, ct);

                foreach (PlacesData.PlaceInfo place in places)
                    CachePlace(place);
            }

            places.AddRange(alreadyCachedPlaces);

            return places;
        }

        public async UniTask<IReadOnlyList<PlacesData.PlaceInfo>> GetFavorites(int pageNumber, int pageSize, CancellationToken ct, bool renewCache = false)
        {
            const int CACHE_EXPIRATION = 30; // Seconds

            // We need to pass the source to avoid conflicts with parallel calls forcing renewCache
            async UniTask RetrieveFavorites(UniTaskCompletionSource<PlacesData.IPlacesAPIResponse> source)
            {
                PlacesData.IPlacesAPIResponse favorites;

                // We dont use the ct param, otherwise the whole flow would be cancel if the first call is cancelled
                if (pageNumber == -1 && pageSize == -1) favorites = await client.GetAllFavorites(ct);
                else { favorites = await client.GetFavorites(pageNumber, pageSize, disposeCts.Token); }

                foreach (PlacesData.PlaceInfo place in favorites.Data)
                    CachePlace(place);

                composedFavoritesDirty = true;
                source.TrySetResult(favorites);
            }

            if (serverFavoritesCompletionSource == null || renewCache || DateTime.Now - serverFavoritesLastRetrieval > TimeSpan.FromSeconds(CACHE_EXPIRATION))
            {
                localFavorites.Clear();
                serverFavoritesLastRetrieval = DateTime.Now;
                serverFavoritesCompletionSource = new UniTaskCompletionSource<PlacesData.IPlacesAPIResponse>();
                RetrieveFavorites(serverFavoritesCompletionSource).Forget();
            }

            using PlacesData.IPlacesAPIResponse serverFavorites = await serverFavoritesCompletionSource.Task.AttachExternalCancellation(ct);

            if (!composedFavoritesDirty)
                return composedFavorites;

            composedFavorites.Clear();

            foreach (PlacesData.PlaceInfo serverFavorite in serverFavorites.Data)
            {
                //skip if it's already in the local favorites cache, it will be added (or not) later
                if (localFavorites.ContainsKey(serverFavorite.id))
                    continue;

                composedFavorites.Add(serverFavorite);
            }

            foreach ((string placeUUID, bool isFavorite) in localFavorites)
            {
                if (!isFavorite)
                    continue;

                if (placesById.TryGetValue(placeUUID, out PlacesData.PlaceInfo place))
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
            PlacesData.PlaceInfo place = await GetPlace(coords, ct);
            await SetPlaceFavorite(place.id, isFavorite, ct);
        }

        public async UniTask<bool> IsFavoritePlace(PlacesData.PlaceInfo placeInfo, CancellationToken ct, bool renewCache = false)
        {
            IReadOnlyList<PlacesData.PlaceInfo> favorites = await GetFavorites(-1, -1, ct, renewCache);

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
            (PlacesData.PlaceInfo placeInfo, IReadOnlyList<PlacesData.PlaceInfo> favorites) = await UniTask.WhenAll(GetPlace(coords, ct, renewCache), GetFavorites(0, 1000, ct, renewCache));

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
            (PlacesData.PlaceInfo placeInfo, IReadOnlyList<PlacesData.PlaceInfo> favorites) = await UniTask.WhenAll(GetPlace(placeUUID, ct, renewCache), GetFavorites(0, 1000, ct, renewCache));

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
