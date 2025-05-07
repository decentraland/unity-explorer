using Cysharp.Threading.Tasks;
using DCL.EventsApi;
using DCL.MapRenderer.Culling;
using DCL.MapRenderer.MapLayers.Cluster;
using DCL.Navmap;
using NBitcoin;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Pool;
using Utility;
using ICoordsUtils = DCL.MapRenderer.CoordsUtils.ICoordsUtils;

namespace DCL.MapRenderer.MapLayers.Categories
{
    internal class LiveEventsMarkersController : MapLayerControllerBase, IMapCullingListener<ICategoryMarker>, IMapLayerController, IZoomScalingLayer
    {
        public bool ZoomBlocked { get; set; }

        private static readonly TimeSpan LIVE_EVENTS_POLLING_TIME = TimeSpan.FromMinutes(5);

        internal delegate ICategoryMarker CategoryMarkerBuilder(
            IObjectPool<CategoryMarkerObject> objectsPool,
            IMapCullingController cullingController,
            ICoordsUtils coordsUtils);

        private readonly MapLayer mapLayer;
        private readonly IObjectPool<CategoryMarkerObject> objectsPool;
        private readonly CategoryMarkerBuilder builder;
        private readonly CategoryIconMappingsSO categoryIconMappings;
        private readonly IEventsApiService eventsApiService;
        private readonly ClusterController clusterController;
        private readonly INavmapBus navmapBus;
        private readonly Dictionary<Vector2Int, IClusterableMarker> markers = new ();
        private readonly Dictionary<GameObject, ICategoryMarker> visibleMarkers = new ();

        private Vector2Int decodePointer;
        private CancellationTokenSource? pollDataCancellationToken;
        private CancellationTokenSource highlightCt = new ();
        private CancellationTokenSource deHighlightCt = new ();
        private ICategoryMarker? previousMarker;
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
            ClusterController clusterController,
            INavmapBus navmapBus)
            : base(instantiationParent, coordsUtils, cullingController)
        {
            this.eventsApiService = eventsApiService;
            this.objectsPool = objectsPool;
            this.builder = builder;
            this.categoryIconMappings = categoryIconMappings;
            this.mapLayer = mapLayer;
            this.clusterController = clusterController;
            this.navmapBus = navmapBus;
        }

        public async UniTask InitializeAsync(CancellationToken cancellationToken)
        {
        }

        public void ApplyCameraZoom(float baseZoom, float zoom, int zoomLevel)
        {
            if (ZoomBlocked)
                return;

            this.baseZoom = baseZoom;
            this.zoom = zoom;
            this.zoomLevel = zoomLevel;

            if (isEnabled && !ZoomBlocked)
                foreach (ICategoryMarker clusterableMarker in clusterController.UpdateClusters(zoomLevel, baseZoom, zoom, markers))
                    mapCullingController.StartTracking(clusterableMarker, this);

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

        public UniTask EnableAsync(CancellationToken cancellationToken)
        {
            if (!arePlacesLoaded)
            {
                pollDataCancellationToken = pollDataCancellationToken.SafeRestart();
                PollEventsAndPlacesOverTimeAsync(pollDataCancellationToken.Token).Forget();
                arePlacesLoaded = true;
            }

            foreach (ICategoryMarker marker in markers.Values)
                mapCullingController.StartTracking(marker, this);

            isEnabled = true;

            if (!ZoomBlocked)
                foreach (ICategoryMarker clusterableMarker in clusterController.UpdateClusters(zoomLevel, baseZoom, zoom, markers))
                    mapCullingController.StartTracking(clusterableMarker, this);

            return UniTask.CompletedTask;
        }

        public void ResetToBaseScale()
        {
            foreach (var marker in markers.Values)
                marker.ResetScale(coordsUtils.ParcelSize);

            clusterController.ResetToBaseScale();
        }

        protected override void DisposeImpl()
        {
            objectsPool.Clear();

            foreach (ICategoryMarker marker in markers.Values)
                marker.Dispose();

            markers.Clear();
            pollDataCancellationToken.SafeCancelAndDispose();
            arePlacesLoaded = false;
        }

        public void OnMapObjectBecameVisible(ICategoryMarker marker)
        {
            marker.OnBecameVisible();
            GameObject? gameObject = marker.GetGameObject();
            if(gameObject != null)
                visibleMarkers.AddOrReplace(gameObject, marker);
        }

        public void OnMapObjectCulled(ICategoryMarker marker)
        {
            GameObject? gameObject = marker.GetGameObject();
            if(gameObject != null)
                visibleMarkers.Remove(gameObject);
            marker.OnBecameInvisible();
        }

        public bool TryHighlightObject(GameObject gameObject, out IMapRendererMarker? mapMarker)
        {
            mapMarker = null;
            if (clusterController.HighlightObject(gameObject))
                return true;

            if (visibleMarkers.TryGetValue(gameObject, out ICategoryMarker marker))
            {
                mapMarker = marker;
                highlightCt = highlightCt.SafeRestart();
                previousMarker?.AnimateDeSelectionAsync(deHighlightCt.Token);
                marker.AnimateSelectionAsync(highlightCt.Token);
                previousMarker = marker;
                return true;
            }

            return false;
        }

        public bool TryDeHighlightObject(GameObject gameObject)
        {
            if (clusterController.DeHighlightObject(gameObject))
                return true;

            previousMarker = null;

            if (visibleMarkers.TryGetValue(gameObject, out ICategoryMarker marker))
            {
                deHighlightCt = deHighlightCt.SafeRestart();
                marker.AnimateDeSelectionAsync(deHighlightCt.Token);
                return true;
            }

            return false;
        }

        public bool TryClickObject(GameObject gameObject, CancellationTokenSource cts, out IMapRendererMarker? mapRenderMarker)
        {
            mapRenderMarker = null;
            if (clusterController.ClickObject(gameObject))
                return true;

            if (visibleMarkers.TryGetValue(gameObject, out ICategoryMarker marker))
            {
                marker.ToggleSelection(true);
                navmapBus.SelectEventAsync(marker.EventDTO, cts.Token, null).Forget();
                mapRenderMarker = marker;
                return true;
            }

            return false;
        }

        private async UniTask PollEventsAndPlacesOverTimeAsync(CancellationToken ct)
        {
            do
            {
                foreach (ICategoryMarker marker in markers.Values)
                {
                    mapCullingController.StopTracking(marker);
                    marker.OnBecameInvisible();
                }
                markers.Clear();
                clusterController.Disable();

                IReadOnlyList<EventDTO> events = await eventsApiService.GetEventsAsync(ct, onlyLiveEvents: true);
                foreach (EventDTO eventDto in events)
                {
                    Vector2Int coords = new Vector2Int(eventDto.x, eventDto.y);
                    if (markers.ContainsKey(coords))
                        continue;

                    ICategoryMarker marker = builder(objectsPool, mapCullingController, coordsUtils);
                    marker.SetCategorySprite(categoryIconMappings.GetCategoryImage(mapLayer));
                    marker.SetData(eventDto.name, coordsUtils.CoordsToPosition(coords), null, eventDto);
                    markers.Add(coords, marker);

                    if(isEnabled)
                        mapCullingController.StartTracking(marker, this);
                }

                if(isEnabled && !ZoomBlocked)
                    foreach (ICategoryMarker clusterableMarker in clusterController.UpdateClusters(zoomLevel, baseZoom, zoom, markers))
                        mapCullingController.StartTracking(clusterableMarker, this);
                await UniTask.Delay(LIVE_EVENTS_POLLING_TIME, DelayType.Realtime, cancellationToken: ct);
            }
            while (ct.IsCancellationRequested == false);
        }
    }
}
