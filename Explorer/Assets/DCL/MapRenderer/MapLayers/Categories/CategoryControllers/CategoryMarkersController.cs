using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Ipfs;
using DCL.MapRenderer.Culling;
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

namespace DCL.MapRenderer.MapLayers.Categories
{
    internal class CategoryMarkersController : MapLayerControllerBase, IMapCullingListener<ICategoryMarker>, IMapLayerController, IZoomScalingLayer
    {
        private const string EMPTY_PARCEL_NAME = "Empty parcel";
        private MapLayer mapLayer;

        private static readonly PoolExtensions.Scope<List<PlacesData.PlaceInfo>> EMPTY_PLACES = PoolExtensions.EmptyScope(new List<PlacesData.PlaceInfo>());

        internal delegate ICategoryMarker CategoryMarkerBuilder(
            IObjectPool<CategoryMarkerObject> objectsPool,
            IMapCullingController cullingController);

        private readonly IObjectPool<CategoryMarkerObject> objectsPool;
        private readonly CategoryMarkerBuilder builder;
        private readonly CategoryIconMappingsSO categoryIconMappings;
        private readonly IPlacesAPIService placesAPIService;

        private readonly Dictionary<PlacesData.PlaceInfo, ICategoryMarker> markers = new ();
        private readonly List<Vector2Int> vectorCoords = new ();
        private Vector2Int decodePointer;

        private bool isEnabled;

        public CategoryMarkersController(
            IPlacesAPIService placesAPIService,
            IObjectPool<CategoryMarkerObject> objectsPool,
            CategoryMarkerBuilder builder,
            Transform instantiationParent,
            ICoordsUtils coordsUtils,
            IMapCullingController cullingController,
            CategoryIconMappingsSO categoryIconMappings,
            MapLayer mapLayer)
            : base(instantiationParent, coordsUtils, cullingController)
        {
            this.placesAPIService = placesAPIService;
            this.objectsPool = objectsPool;
            this.builder = builder;
            this.categoryIconMappings = categoryIconMappings;
        }

        public async UniTask InitializeAsync(CancellationToken cancellationToken)
        {
            IReadOnlyList<string> artPlace =
                await placesAPIService.GetPointsOfInterestCoordsAsync(cancellationToken)
                                      .SuppressAnyExceptionWithFallback(Array.Empty<string>(), ReportCategory.UI);
            vectorCoords.Clear();

            foreach (var s in artPlace)
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
                if (markers.ContainsKey(placeInfo))
                    continue;

                if (IsEmptyParcel(placeInfo))
                    continue;

                var marker = builder(objectsPool, mapCullingController);
                var centerParcel = GetParcelsCenter(placeInfo);
                var position = coordsUtils.CoordsToPosition(centerParcel);

                marker.SetData(placeInfo.title, position);
                marker.SetCategorySprite(categoryIconMappings.GetCategoryImage(mapLayer));
                markers.Add(placeInfo, marker);

                if (isEnabled)
                    mapCullingController.StartTracking(marker, this);
            }
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
            foreach (ICategoryMarker marker in markers.Values)
                marker.SetZoom(coordsUtils.ParcelSize, baseZoom, zoom);
        }

        public void ResetToBaseScale()
        {
            foreach (var marker in markers.Values)
                marker.ResetScale(coordsUtils.ParcelSize);
        }

        public UniTask Disable(CancellationToken cancellationToken)
        {
            // Make markers invisible to release everything to the pool and stop tracking
            foreach (ICategoryMarker marker in markers.Values)
            {
                mapCullingController.StopTracking(marker);
                marker.OnBecameInvisible();
            }

            isEnabled = false;

            return UniTask.CompletedTask;
        }

        public UniTask Enable(CancellationToken cancellationToken)
        {
            foreach (ICategoryMarker marker in markers.Values)
                mapCullingController.StartTracking(marker, this);

            isEnabled = true;

            return UniTask.CompletedTask;
        }
    }
}
