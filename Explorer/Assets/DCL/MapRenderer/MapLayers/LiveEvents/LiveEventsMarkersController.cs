using Cysharp.Threading.Tasks;
using DCL.EventsApi;
using DCL.MapRenderer.Culling;
using DCL.PlacesAPIService;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Pool;
using ICoordsUtils = DCL.MapRenderer.CoordsUtils.ICoordsUtils;

namespace DCL.MapRenderer.MapLayers.Categories
{
    internal class LiveEventsMarkersController : MapLayerControllerBase, IMapCullingListener<ICategoryMarker>, IMapLayerController, IZoomScalingLayer
    {
        private static readonly TimeSpan LIVE_EVENTS_POLLING_TIME = TimeSpan.FromMinutes(5);
        private readonly MapLayer mapLayer;

        internal delegate ICategoryMarker CategoryMarkerBuilder(
            IObjectPool<CategoryMarkerObject> objectsPool,
            IMapCullingController cullingController,
            ICoordsUtils coordsUtils);

        private readonly IObjectPool<CategoryMarkerObject> objectsPool;
        private readonly CategoryMarkerBuilder builder;
        private readonly CategoryIconMappingsSO categoryIconMappings;
        private readonly IEventsApiService eventsApiService;
        private readonly ClusterController clusterController;

        private readonly Dictionary<Vector2Int, IClusterableMarker> markers = new();

        private Vector2Int decodePointer;
        private bool isEnabled;
        private int zoomLevel = 1;
        private float baseZoom = 1;
        private float zoom = 1;
        private bool arePlacesLoaded;

        public LiveEventsMarkersController(
            IEventsApiService eventsApiService,
            IObjectPool<CategoryMarkerObject> objectsPool,
            CategoryMarkerBuilder builder,
            Transform instantiationParent,
            ICoordsUtils coordsUtils,
            IMapCullingController cullingController,
            CategoryIconMappingsSO categoryIconMappings,
            MapLayer mapLayer,
            ClusterController clusterController)
            : base(instantiationParent, coordsUtils, cullingController)
        {
            this.eventsApiService = eventsApiService;
            this.objectsPool = objectsPool;
            this.builder = builder;
            this.categoryIconMappings = categoryIconMappings;
            this.mapLayer = mapLayer;
            this.clusterController = clusterController;
        }

        public UniTask InitializeAsync(CancellationToken cancellationToken)
        {
            LoadEventsPlacesAsync(cancellationToken).Forget();
            return UniTask.CompletedTask;
        }

        private async UniTaskVoid LoadEventsPlacesAsync(CancellationToken ct)
        {
            do
            {
                foreach (ICategoryMarker marker in markers.Values)
                {
                    mapCullingController.StopTracking(marker);
                    marker.OnBecameInvisible();
                }
                markers.Clear();
                IReadOnlyList<EventDTO> events = await eventsApiService.GetEventsAsync(ct, onlyLiveEvents: true);
                foreach (EventDTO eventDto in events)
                {
                    Vector2Int coords = new Vector2Int(eventDto.x, eventDto.y);
                    if (markers.ContainsKey(coords))
                        continue;

                    ICategoryMarker marker = builder(objectsPool, mapCullingController, coordsUtils);
                    marker.SetCategorySprite(categoryIconMappings.GetCategoryImage(mapLayer));
                    marker.SetData(eventDto.name, coordsUtils.CoordsToPosition(coords));
                    markers.Add(coords, marker);
                    if(isEnabled)
                        mapCullingController.StartTracking(marker, this);
                }
                await UniTask.Delay(LIVE_EVENTS_POLLING_TIME, DelayType.Realtime, cancellationToken: ct);
            }
            while (ct.IsCancellationRequested == false);
        }

        public void ApplyCameraZoom(float baseZoom, float zoom, int zoomLevel)
        {
            this.baseZoom = baseZoom;
            this.zoom = zoom;
            this.zoomLevel = zoomLevel;

            if (isEnabled)
                clusterController.UpdateClusters(zoomLevel, baseZoom, zoom, markers);

            foreach (ICategoryMarker marker in markers.Values)
                marker.SetZoom(coordsUtils.ParcelSize, baseZoom, zoom);

            clusterController.ApplyCameraZoom(baseZoom, zoom);
        }

        public UniTask Disable(CancellationToken cancellationToken)
        {
            foreach (ICategoryMarker marker in markers.Values)
            {
                mapCullingController.StopTracking(marker);
                marker.OnBecameInvisible();
            }

            clusterController.Disable();

            isEnabled = false;
            return UniTask.CompletedTask;
        }

        public UniTask Enable(CancellationToken cancellationToken)
        {
            foreach (ICategoryMarker marker in markers.Values)
                mapCullingController.StartTracking(marker, this);

            isEnabled = true;
            clusterController.UpdateClusters(zoomLevel, baseZoom, zoom, markers);
            return UniTask.CompletedTask;
        }

        public void ResetToBaseScale()
        {
            foreach (var marker in markers.Values)
                marker.ResetScale(coordsUtils.ParcelSize);
        }

        protected override void DisposeImpl()
        {
            objectsPool.Clear();

            foreach (ICategoryMarker marker in markers.Values)
                marker.Dispose();

            markers.Clear();
        }

        public void OnMapObjectBecameVisible(ICategoryMarker marker)
        {
            marker.OnBecameVisible();
        }

        public void OnMapObjectCulled(ICategoryMarker marker)
        {
            marker.OnBecameInvisible();
        }
    }
}
