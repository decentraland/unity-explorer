using Cysharp.Threading.Tasks;
using DCL.MapRenderer.CoordsUtils;
using DCL.MapRenderer.Culling;
using DCL.MapRenderer.MapLayers.Categories;
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
            IMapCullingController cullingController,
            ICoordsUtils coordsUtils);

        private readonly IPlacesAPIService placesAPIService;
        private readonly IObjectPool<FavoriteMarkerObject> objectsPool;
        private readonly FavoritesMarkerBuilder builder;
        private readonly ClusterController clusterController;

        private readonly Dictionary<Vector2Int, IClusterableMarker> markers = new ();

        private bool isEnabled;
        private int zoomLevel = 1;
        private float baseZoom = 1;
        private float zoom = 1;

        public FavoritesMarkerController(
            IPlacesAPIService placesAPIService,
            IObjectPool<FavoriteMarkerObject> objectsPool,
            FavoritesMarkerBuilder builder,
            Transform instantiationParent,
            ICoordsUtils coordsUtils,
            IMapCullingController cullingController,
            ClusterController clusterController)
            : base(instantiationParent, coordsUtils, cullingController)
        {
            this.placesAPIService = placesAPIService;
            this.objectsPool = objectsPool;
            this.builder = builder;
            this.clusterController = clusterController;
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
            using var favoritePlaces = await placesAPIService.GetFavoritesAsync(cancellationToken);

            foreach (PlacesData.PlaceInfo placeInfo in favoritePlaces.Data)
                OnMinimapSceneInfoUpdated(placeInfo);
        }

        public void OnMapObjectCulled(IFavoritesMarker marker)
        {
            marker.OnBecameInvisible();
        }

        public void ApplyCameraZoom(float baseZoom, float zoom, int zoomLevel)
        {
            this.baseZoom = baseZoom;
            this.zoom = zoom;
            this.zoomLevel = zoomLevel;

            foreach (IFavoritesMarker marker in markers.Values)
                marker.SetZoom(coordsUtils.ParcelSize, baseZoom, zoom);

            if (isEnabled)
                clusterController.UpdateClusters(zoomLevel, baseZoom, zoom, markers);

            clusterController.ApplyCameraZoom(baseZoom, zoom);
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
            if (markers.ContainsKey(MapLayerUtils.GetParcelsCenter(sceneInfo)))
                return;

            if (IsEmptyParcel(sceneInfo))
                return;

            var marker = builder(objectsPool, mapCullingController, coordsUtils);

            var centerParcel = MapLayerUtils.GetParcelsCenter(sceneInfo);
            var position = coordsUtils.CoordsToPosition(centerParcel);

            marker.SetData(sceneInfo.title, position);

            markers.Add(MapLayerUtils.GetParcelsCenter(sceneInfo), marker);

            if (isEnabled)
                mapCullingController.StartTracking(marker, this);
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
            clusterController.Disable();

            isEnabled = false;

            return UniTask.CompletedTask;
        }

        public UniTask InitializeAsync(CancellationToken cancellationToken) =>
            UniTask.CompletedTask;

        public UniTask Enable(CancellationToken cancellationToken)
        {
            GetFavoritesAsync(CancellationToken.None).Forget();
            foreach (IFavoritesMarker marker in markers.Values)
                mapCullingController.StartTracking(marker, this);

            isEnabled = true;
            clusterController.UpdateClusters(zoomLevel, baseZoom, zoom, markers);
            return UniTask.CompletedTask;
        }
    }
}
