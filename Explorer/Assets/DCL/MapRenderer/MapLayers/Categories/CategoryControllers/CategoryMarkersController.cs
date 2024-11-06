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
        private readonly MapLayer mapLayer;

        internal delegate ICategoryMarker CategoryMarkerBuilder(
            IObjectPool<CategoryMarkerObject> objectsPool,
            IMapCullingController cullingController);

        private readonly IObjectPool<CategoryMarkerObject> objectsPool;
        private readonly CategoryMarkerBuilder builder;
        private readonly CategoryIconMappingsSO categoryIconMappings;
        private readonly IPlacesAPIService placesAPIService;

        private readonly Dictionary<PlacesData.CategoryPlaceData, ICategoryMarker> markers = new ();
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
            this.mapLayer = mapLayer;
        }

        public async UniTask InitializeAsync(CancellationToken cancellationToken)
        {
            List<PlacesData.CategoryPlaceData> placesOfCategory = await placesAPIService.GetPlacesByCategoryListAsync(MapLayerUtils.MapLayerToCategory[mapLayer], cancellationToken);

            foreach (PlacesData.CategoryPlaceData placeInfo in placesOfCategory)
            {
                if (markers.ContainsKey(placeInfo))
                    continue;

                if (IsEmptyParcel(placeInfo))
                    continue;

                var marker = builder(objectsPool, mapCullingController);
                var position = coordsUtils.CoordsToPosition(placeInfo.base_position);

                marker.SetData(placeInfo.name, position);
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

        private static bool IsEmptyParcel(PlacesData.CategoryPlaceData sceneInfo) =>
            sceneInfo.name is EMPTY_PARCEL_NAME;

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
