using Cysharp.Threading.Tasks;
using DCL.MapRenderer.CoordsUtils;
using DCL.MapRenderer.Culling;
using DCL.PlacesAPIService;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Pool;

namespace DCL.MapRenderer.MapLayers.Favorites
{
    internal class FavoritesMarkerController : MapLayerControllerBase, IMapCullingListener<IFavoritesMarker>, IMapLayerController, IZoomScalingLayer
    {
        private const string EMPTY_PARCEL_NAME = "Empty parcel";

        internal delegate IFavoritesMarker FavoritesMarkerBuilder(
            IObjectPool<FavoriteMarkerObject> objectsPool,
            IMapCullingController cullingController);

        private readonly IPlacesAPIService placesAPIService;
        private readonly IObjectPool<FavoriteMarkerObject> objectsPool;
        private readonly FavoritesMarkerBuilder builder;

        private readonly Dictionary<PlacesData.PlaceInfo, IFavoritesMarker> markers = new ();

        private bool isEnabled;

        public FavoritesMarkerController(
            IPlacesAPIService placesAPIService,
            IObjectPool<FavoriteMarkerObject> objectsPool,
            FavoritesMarkerBuilder builder,
            Transform instantiationParent,
            ICoordsUtils coordsUtils,
            IMapCullingController cullingController)
            : base(instantiationParent, coordsUtils, cullingController)
        {
            this.placesAPIService = placesAPIService;
            this.objectsPool = objectsPool;
            this.builder = builder;
        }

        protected override void DisposeImpl()
        {
            objectsPool.Clear();

            foreach (IFavoritesMarker marker in markers.Values)
                marker.Dispose();

            markers.Clear();
        }

        public void OnMapObjectBecameVisible(IFavoritesMarker marker)
        {
            marker.OnBecameVisible();
        }

        private async UniTaskVoid GetFavoritesAsync(CancellationToken cancellationToken)
        {
            using var favoritePlaces = await placesAPIService.GetFavoritesAsync(-1, -1, cancellationToken);
            foreach (PlacesData.PlaceInfo placeInfo in favoritePlaces.Value)
                OnMinimapSceneInfoUpdated(placeInfo);
        }

        public void OnMapObjectCulled(IFavoritesMarker marker)
        {
            marker.OnBecameInvisible();
        }

        public void ApplyCameraZoom(float baseZoom, float zoom)
        {
            foreach (IFavoritesMarker marker in markers.Values)
                marker.SetZoom(coordsUtils.ParcelSize, baseZoom, zoom);
        }

        public void ResetToBaseScale()
        {
            foreach (var marker in markers.Values)
                marker.ResetScale(coordsUtils.ParcelSize);
        }

        private void OnMinimapSceneInfoUpdated(PlacesData.PlaceInfo sceneInfo)
        {
            // Markers are not really updated, they can be just reported several times with essentially the same data
            if (!sceneInfo.user_favorite)
                return;

            // if it was possible to update them then we need to cache by parcel coordinates instead
            // and recalculate the parcels centers accordingly
            if (markers.ContainsKey(sceneInfo))
                return;

            if (IsEmptyParcel(sceneInfo))
                return;

            var marker = builder(objectsPool, mapCullingController);

            var centerParcel = GetParcelsCenter(sceneInfo);
            var position = coordsUtils.CoordsToPosition(centerParcel, marker);

            marker.SetData(sceneInfo.title, position);

            markers.Add(sceneInfo, marker);

            if (isEnabled)
                mapCullingController.StartTracking(marker, this);
        }

        private static Vector2Int GetParcelsCenter(PlacesData.PlaceInfo sceneInfo)
        {
            Vector2 centerTile = Vector2.zero;

            for (var i = 0; i < sceneInfo.Positions.Length; i++)
            {
                Vector2Int parcel = sceneInfo.Positions[i];
                centerTile += parcel;
            }

            centerTile /= sceneInfo.Positions.Length;
            float distance = float.PositiveInfinity;
            Vector2Int centerParcel = Vector2Int.zero;

            for (var i = 0; i < sceneInfo.Positions.Length; i++)
            {
                var parcel = sceneInfo.Positions[i];

                if (Vector2.Distance(centerTile, parcel) < distance)
                {
                    distance = Vector2Int.Distance(centerParcel, parcel);
                    centerParcel = parcel;
                }
            }

            return centerParcel;
        }

        private static bool IsEmptyParcel(PlacesData.PlaceInfo sceneInfo) =>
            sceneInfo.title is EMPTY_PARCEL_NAME;

        public UniTask Disable(CancellationToken cancellationToken)
        {
            // Make markers invisible to release everything to the pool and stop tracking
            foreach (IFavoritesMarker marker in markers.Values)
            {
                mapCullingController.StopTracking(marker);
                marker.OnBecameInvisible();
            }

            isEnabled = false;

            return UniTask.CompletedTask;
        }

        public UniTask Enable(CancellationToken cancellationToken)
        {
            GetFavoritesAsync(CancellationToken.None).Forget();
            foreach (IFavoritesMarker marker in markers.Values)
                mapCullingController.StartTracking(marker, this);

            isEnabled = true;

            return UniTask.CompletedTask;
        }
    }
}
