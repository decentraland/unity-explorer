using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Ipfs;
using DCL.MapRenderer.Culling;
using DCL.MapRenderer.MapLayers.Categories;
using DCL.Optimization.Pools;
using DCL.Utilities.Extensions;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Pool;
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
        private readonly IPlacesAPIService placesAPIService;

        private readonly Dictionary<Vector2Int, IClusterableMarker> markers = new ();
        private readonly List<Vector2Int> vectorCoords = new ();
        private Vector2Int decodePointer;

        private bool isEnabled;
        private float clusterCellSize;
        private float baseZoom;
        private float zoom;

        public ScenesOfInterestMarkersController(
            IPlacesAPIService placesAPIService,
            IObjectPool<SceneOfInterestMarkerObject> objectsPool,
            SceneOfInterestMarkerBuilder builder,
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
                if (markers.ContainsKey(GetParcelsCenter(placeInfo)))
                    continue;

                if (IsEmptyParcel(placeInfo))
                    continue;

                var marker = builder(objectsPool, mapCullingController, coordsUtils);
                var centerParcel = GetParcelsCenter(placeInfo);
                var position = coordsUtils.CoordsToPosition(centerParcel);

                marker.SetData(placeInfo.title, position);
                markers.Add(GetParcelsCenter(placeInfo), marker);

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
        }

        public void OnMapObjectCulled(ISceneOfInterestMarker marker)
        {
            marker.OnBecameInvisible();
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

        public void ApplyCameraZoom(float baseZoom, float zoom)
        {
            this.baseZoom = baseZoom;
            this.zoom = zoom;
            clusterCellSize = ClusterUtilities.CalculateCellSize(zoom);

            foreach (ISceneOfInterestMarker marker in markers.Values)
                marker.SetZoom(coordsUtils.ParcelSize, baseZoom, zoom);

            if(isEnabled)
                clusterController.UpdateClusters(clusterCellSize, baseZoom, zoom, markers);

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

            isEnabled = false;

            return UniTask.CompletedTask;
        }

        public UniTask Enable(CancellationToken cancellationToken)
        {
            foreach (ISceneOfInterestMarker marker in markers.Values)
                mapCullingController.StartTracking(marker, this);

            isEnabled = true;
            clusterController.UpdateClusters(clusterCellSize, baseZoom, zoom, markers);
            return UniTask.CompletedTask;
        }
    }
}
