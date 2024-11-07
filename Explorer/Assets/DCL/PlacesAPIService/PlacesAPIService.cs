﻿using Cysharp.Threading.Tasks;
using DCL.Optimization.Pools;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace DCL.PlacesAPIService
{
    public partial class PlacesAPIService : IPlacesAPIService
    {
        private static readonly ListObjectPool<Vector2Int> COORDS_TO_REQ_POOL = new ();

        internal readonly Dictionary<string, PlacesData.PlaceInfo> placesById = new ();
        internal readonly Dictionary<Vector2Int, PlacesData.PlaceInfo> placesByCoords = new ();
        internal readonly Dictionary<string, bool> localFavorites = new ();

        private readonly IPlacesAPIClient client;

        private readonly CancellationTokenSource disposeCts = new ();

        //Favorites
        internal bool composedFavoritesDirty = true;
        internal UniTaskCompletionSource<PlacesData.IPlacesAPIResponse>? serverFavoritesCompletionSource;
        private List<string>? pointsOfInterestCoords;
        private DateTime serverFavoritesLastRetrieval = DateTime.MinValue;

        public PlacesAPIService(IPlacesAPIClient client)
        {
            this.client = client;
        }

        public async UniTask<PlacesData.IPlacesAPIResponse> SearchPlacesAsync(string searchText, int pageNumber, int pageSize,
            CancellationToken ct,
            IPlacesAPIService.SortBy sortByBy = IPlacesAPIService.SortBy.MOST_ACTIVE,
            IPlacesAPIService.SortDirection sortDirection = IPlacesAPIService.SortDirection.DESC) =>
            await client.SearchPlacesAsync(searchText, pageNumber, pageSize, ct,
                sortByBy.ToString().ToLower(), sortDirection.ToString().ToLower());

        public async UniTask<PlacesData.PlaceInfo?> GetPlaceAsync(Vector2Int coords, CancellationToken ct, bool renewCache = false)
        {
            if (renewCache)
                placesByCoords.Remove(coords);
            else if (placesByCoords.TryGetValue(coords, out PlacesData.PlaceInfo placeInfo))
                return placeInfo;

            PlacesData.PlaceInfo? place = await client.GetPlaceAsync(coords, ct);
            TryCachePlace(place);
            return place;
        }

        public async UniTask<PlacesData.PlaceInfo?> GetPlaceAsync(string placeUUID, CancellationToken ct, bool renewCache = false)
        {
            if (renewCache)
                placesById.Remove(placeUUID);
            else if (placesById.TryGetValue(placeUUID, out PlacesData.PlaceInfo placeInfo))
                return placeInfo;

            PlacesData.PlaceInfo? place = await client.GetPlaceAsync(placeUUID, ct);
            TryCachePlace(place);
            return place;
        }

        public async UniTask<PoolExtensions.Scope<List<PlacesData.PlaceInfo>>> GetPlacesByCoordsListAsync(IEnumerable<Vector2Int> coordsList, CancellationToken ct, bool renewCache = false)
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

            PoolExtensions.Scope<List<PlacesData.PlaceInfo>> rentedPlaces = PlacesData.PLACE_INFO_LIST_POOL.AutoScope();
            List<PlacesData.PlaceInfo> places = rentedPlaces.Value;

            if (coordsToRequest.Value.Count > 0)
            {
                places = await client.GetPlacesByCoordsListAsync(coordsToRequest.Value, places, ct);

                foreach (PlacesData.PlaceInfo place in places)
                    TryCachePlace(place);
            }

            places.AddRange(alreadyCachedPlaces);
            return rentedPlaces;
        }

        public async UniTask<PoolExtensions.Scope<List<PlacesData.PlaceInfo>>> GetFavoritesAsync(int pageNumber, int pageSize, CancellationToken ct, bool renewCache = false,
            IPlacesAPIService.SortBy sortByBy = IPlacesAPIService.SortBy.MOST_ACTIVE,
            IPlacesAPIService.SortDirection sortDirection = IPlacesAPIService.SortDirection.DESC)
        {
            const int CACHE_EXPIRATION = 30; // Seconds

            if (serverFavoritesCompletionSource == null || renewCache || DateTime.Now - serverFavoritesLastRetrieval > TimeSpan.FromSeconds(CACHE_EXPIRATION))
            {
                localFavorites.Clear();
                serverFavoritesLastRetrieval = DateTime.Now;
                serverFavoritesCompletionSource = new UniTaskCompletionSource<PlacesData.IPlacesAPIResponse>();
                RetrieveFavoritesAsync(serverFavoritesCompletionSource).Forget();
            }

            using PlacesData.IPlacesAPIResponse serverFavorites = await serverFavoritesCompletionSource.Task.AttachExternalCancellation(ct);
            PoolExtensions.Scope<List<PlacesData.PlaceInfo>> rentedPlaces = PlacesData.PLACE_INFO_LIST_POOL.AutoScope();
            List<PlacesData.PlaceInfo> places = rentedPlaces.Value;

            if (!composedFavoritesDirty)
                return rentedPlaces;

            foreach (PlacesData.PlaceInfo serverFavorite in serverFavorites.Data)
            {
                //skip if it's already in the local favorites cache, it will be added (or not) later
                if (localFavorites.ContainsKey(serverFavorite.id))
                    continue;

                places.Add(serverFavorite);
            }

            foreach ((string placeUUID, bool isFavorite) in localFavorites)
            {
                if (!isFavorite)
                    continue;

                if (placesById.TryGetValue(placeUUID, out PlacesData.PlaceInfo place))
                    places.Add(place);
            }

            composedFavoritesDirty = false;

            return rentedPlaces;

            // We need to pass the source to avoid conflicts with parallel calls forcing renewCache
            async UniTask RetrieveFavoritesAsync(UniTaskCompletionSource<PlacesData.IPlacesAPIResponse> source)
            {
                PlacesData.IPlacesAPIResponse favorites;

                string sortByParam = sortByBy.ToString().ToLower();
                string sortDirectionParam = sortDirection.ToString().ToLower();

                // We dont use the ct param, otherwise the whole flow would be cancel if the first call is cancelled
                if (pageNumber == -1 && pageSize == -1)
                    favorites = await client.GetAllFavoritesAsync(ct, sortByParam, sortDirectionParam);
                else
                    favorites = await client.GetFavoritesAsync(pageNumber, pageSize, disposeCts.Token,
                        sortByParam, sortDirectionParam);

                foreach (PlacesData.PlaceInfo place in favorites.Data)
                    TryCachePlace(place);

                composedFavoritesDirty = true;
                source.TrySetResult(favorites);
            }
        }

        public async UniTask SetPlaceFavoriteAsync(string placeUUID, bool isFavorite, CancellationToken ct)
        {
            localFavorites[placeUUID] = isFavorite;
            composedFavoritesDirty = true;
            await client.SetPlaceFavoriteAsync(placeUUID, isFavorite, ct);
        }

        public async UniTask RatePlace(bool? isUpvote, string placeUUID, CancellationToken ct)
        {
            await client.RatePlace(isUpvote, placeUUID, ct);
        }

        private void TryCachePlace(PlacesData.PlaceInfo? placeInfo)
        {
            if (placeInfo == null)
                return;

            placesById[placeInfo.id] = placeInfo;
            foreach (Vector2Int placeInfoPosition in placeInfo.Positions) { placesByCoords[placeInfoPosition] = placeInfo; }
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
    }
}
