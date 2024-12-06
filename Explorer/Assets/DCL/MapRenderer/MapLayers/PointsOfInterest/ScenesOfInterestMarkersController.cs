﻿using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Ipfs;
using DCL.MapRenderer.Culling;
using DCL.MapRenderer.MapLayers.Cluster;
using DCL.Navmap;
using DCL.Optimization.Pools;
using DCL.Utilities.Extensions;
using NBitcoin;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Pool;
using Utility;
using ICoordsUtils = DCL.MapRenderer.CoordsUtils.ICoordsUtils;
using IPlacesAPIService = DCL.PlacesAPIService.IPlacesAPIService;
using PlacesData = DCL.PlacesAPIService.PlacesData;

namespace DCL.MapRenderer.MapLayers.PointsOfInterest
{
    internal class ScenesOfInterestMarkersController : MapLayerControllerBase, IMapCullingListener<ISceneOfInterestMarker>, IMapLayerController, IZoomScalingLayer
    {
        private const string EMPTY_PARCEL_NAME = "Empty parcel";

        private static readonly PoolExtensions.Scope<List<PlacesData.PlaceInfo>> EMPTY_PLACES = PoolExtensions.EmptyScope(new List<PlacesData.PlaceInfo>());

        internal delegate ISceneOfInterestMarker SceneOfInterestMarkerBuilder(
            IObjectPool<SceneOfInterestMarkerObject> objectsPool,
            IMapCullingController cullingController,
            ICoordsUtils coordsUtils);

        private readonly IObjectPool<SceneOfInterestMarkerObject> objectsPool;
        private readonly SceneOfInterestMarkerBuilder builder;
        private readonly ClusterController clusterController;
        private readonly INavmapBus navmapBus;
        private readonly IPlacesAPIService placesAPIService;

        private readonly Dictionary<Vector2Int, IClusterableMarker> markers = new ();
        private readonly Dictionary<GameObject, ISceneOfInterestMarker> visibleMarkers = new ();
        private readonly List<Vector2Int> vectorCoords = new ();

        private Vector2Int decodePointer;
        private CancellationTokenSource highlightCt = new ();
        private CancellationTokenSource deHighlightCt = new ();
        private ISceneOfInterestMarker? previousMarker;

        private CancellationTokenSource cts = new();

        private bool isEnabled;
        private int zoomLevel = 1;
        private float baseZoom = 1;
        private float zoom = 1;

        public ScenesOfInterestMarkersController(
            IPlacesAPIService placesAPIService,
            IObjectPool<SceneOfInterestMarkerObject> objectsPool,
            SceneOfInterestMarkerBuilder builder,
            Transform instantiationParent,
            ICoordsUtils coordsUtils,
            IMapCullingController cullingController,
            ClusterController clusterController,
            INavmapBus navmapBus)
            : base(instantiationParent, coordsUtils, cullingController)
        {
            this.placesAPIService = placesAPIService;
            this.objectsPool = objectsPool;
            this.builder = builder;
            this.clusterController = clusterController;
            this.navmapBus = navmapBus;
        }

        public async UniTask InitializeAsync(CancellationToken cancellationToken)
        {
            IReadOnlyList<string> pointsOfInterestCoordsAsync =
                await placesAPIService.GetPointsOfInterestCoordsAsync(cancellationToken)
                                      .SuppressAnyExceptionWithFallback(Array.Empty<string>(), ReportCategory.UI);
            vectorCoords.Clear();

            foreach (var s in pointsOfInterestCoordsAsync)
            {
                try { vectorCoords.Add(IpfsHelper.DecodePointer(s)); }
                catch (Exception e) { ReportHub.LogException(e, ReportCategory.UI); }
            }

            using var placesByCoordsListAsync =
                await placesAPIService.GetPlacesByCoordsListAsync(vectorCoords, cancellationToken, true)
                                      .SuppressAnyExceptionWithFallback(EMPTY_PLACES, ReportCategory.UI);

            // non-blocking retrieval of scenes of interest happens independently on the minimap rendering
            foreach (PlacesData.PlaceInfo placeInfo in placesByCoordsListAsync.Value)
            {
                if (markers.ContainsKey(MapLayerUtils.GetParcelsCenter(placeInfo)))
                    continue;

                if (IsEmptyParcel(placeInfo))
                    continue;

                var marker = builder(objectsPool, mapCullingController, coordsUtils);
                var centerParcel = MapLayerUtils.GetParcelsCenter(placeInfo);
                var position = coordsUtils.CoordsToPosition(centerParcel);

                marker.SetData(placeInfo.title, position, placeInfo);
                markers.Add(MapLayerUtils.GetParcelsCenter(placeInfo), marker);

                if (isEnabled)
                    mapCullingController.StartTracking(marker, this);
            }
        }

        protected override void DisposeImpl()
        {
            objectsPool.Clear();

            foreach (ISceneOfInterestMarker marker in markers.Values)
                marker.Dispose();

            markers.Clear();
        }

        public void OnMapObjectBecameVisible(ISceneOfInterestMarker marker)
        {
            marker.OnBecameVisible();
            GameObject? gameObject = marker.GetGameObject();
            if(gameObject != null)
                visibleMarkers.AddOrReplace(gameObject, marker);
        }

        public void OnMapObjectCulled(ISceneOfInterestMarker marker)
        {
            GameObject? gameObject = marker.GetGameObject();
            if(gameObject != null)
                visibleMarkers.Remove(gameObject);
            marker.OnBecameInvisible();
        }

        private static bool IsEmptyParcel(PlacesData.PlaceInfo sceneInfo) =>
            sceneInfo.title is EMPTY_PARCEL_NAME;

        public void ApplyCameraZoom(float baseZoom, float zoom, int zoomLevel)
        {
            this.baseZoom = baseZoom;
            this.zoom = zoom;
            this.zoomLevel = zoomLevel;

            foreach (ISceneOfInterestMarker marker in markers.Values)
                marker.SetZoom(coordsUtils.ParcelSize, baseZoom, zoom);

            if (isEnabled)
                foreach (ISceneOfInterestMarker clusterableMarker in clusterController.UpdateClusters(zoomLevel, baseZoom, zoom, markers))
                    mapCullingController.StartTracking(clusterableMarker, this);

            clusterController.ApplyCameraZoom(baseZoom, zoom);
        }

        public void ResetToBaseScale()
        {
            foreach (var marker in markers.Values)
                marker.ResetScale(coordsUtils.ParcelSize);
        }

        public UniTask Disable(CancellationToken cancellationToken)
        {
            // Make markers invisible to release everything to the pool and stop tracking
            foreach (ISceneOfInterestMarker marker in markers.Values)
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
            foreach (ISceneOfInterestMarker marker in markers.Values)
                mapCullingController.StartTracking(marker, this);

            isEnabled = true;
            foreach (ISceneOfInterestMarker clusterableMarker in clusterController.UpdateClusters(zoomLevel, baseZoom, zoom, markers))
                mapCullingController.StartTracking(clusterableMarker, this);
            return UniTask.CompletedTask;
        }

        public bool HighlightObject(GameObject gameObject)
        {
            if (clusterController.HighlightObject(gameObject))
                return true;

            if (visibleMarkers.TryGetValue(gameObject, out ISceneOfInterestMarker marker))
            {
                highlightCt = highlightCt.SafeRestart();
                previousMarker?.AnimateDeSelectionAsync(deHighlightCt.Token);
                marker.AnimateSelectionAsync(highlightCt.Token);
                previousMarker = marker;
                return true;
            }

            return false;
        }

        public bool DeHighlightObject(GameObject gameObject)
        {
            if (clusterController.DeHighlightObject(gameObject))
                return true;

            previousMarker = null;

            if (visibleMarkers.TryGetValue(gameObject, out ISceneOfInterestMarker marker))
            {
                deHighlightCt = deHighlightCt.SafeRestart();
                marker.AnimateDeSelectionAsync(deHighlightCt.Token);
                return true;
            }

            return false;
        }

        private ISceneOfInterestMarker? previouslyClicked;

        public bool ClickObject(GameObject gameObject)
        {
            if (previouslyClicked != null)
            {
                previouslyClicked.ToggleSelection(false);
                previouslyClicked = null;
            }

            if (clusterController.ClickObject(gameObject))
                return true;

            if (visibleMarkers.TryGetValue(gameObject, out ISceneOfInterestMarker marker))
            {
                previouslyClicked = marker;
                cts = cts.SafeRestart();
                marker.ToggleSelection(true);
                navmapBus.SelectPlaceAsync(marker.PlaceInfo, cts.Token).Forget();
                return true;
            }

            return false;
        }
    }
}
