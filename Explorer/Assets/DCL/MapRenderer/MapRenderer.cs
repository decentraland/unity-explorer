using Arch.Core;
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.MapRenderer.CommonBehavior;
using DCL.MapRenderer.ComponentsFactory;
using DCL.MapRenderer.CoordsUtils;
using DCL.MapRenderer.MapCameraController;
using DCL.MapRenderer.MapLayers;
using DCL.MapRenderer.MapLayers.SatelliteAtlas;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Pool;
using Utility;

namespace DCL.MapRenderer
{
    public partial class MapRenderer : IMapRenderer
    {
        // Layers fed by Genesis City's parcels, places, events and users, hidden while the map shows a world.
        private const MapLayer GENESIS_CITY_LAYERS = MapLayer.ParcelsAtlas | MapLayer.ScenesOfInterest | MapLayer.Favorites | MapLayer.HotUsersMarkers | MapLayer.Category
                                                     | MapLayer.SearchResults | MapLayer.LiveEvents | MapLayer.HomeMarker;

        private static readonly MapLayer[] ALL_LAYERS = EnumUtils.Values<MapLayer>();

        // The sea around a world's terrain in its satellite capture, shown past the world's tiles in place of Genesis City's ocean
        private static readonly Color WORLD_OCEAN_COLOR = new Color32(96, 98, 158, 255);

        private readonly IMapRendererComponentsFactory componentsFactory;
        private readonly List<IMapCameraControllerInternal> rentedCameras = new ();

        private CancellationToken cancellationToken;

        private Dictionary<MapLayer, MapLayerStatus>? layers;
        private List<IZoomScalingLayer>? zoomScalingLayers;
        private IObjectPool<IMapCameraControllerInternal>? mapCameraPool;
        private ICoordsUtils? coordsUtils;
        private SatelliteChunkAtlasController? satelliteAtlas;
        private Color? backgroundColor;

        public MapRenderer(IMapRendererComponentsFactory componentsFactory)
        {
            this.componentsFactory = componentsFactory;
        }

        public async UniTask InitializeAsync(CancellationToken ct)
        {
            cancellationToken = ct;
            layers = new Dictionary<MapLayer, MapLayerStatus>();
            zoomScalingLayers = new List<IZoomScalingLayer>();

            try
            {
                MapRendererComponents components = await componentsFactory.CreateAsync(ct);
                mapCameraPool = components.MapCameraControllers;
                coordsUtils = components.CoordsUtils;
                satelliteAtlas = components.SatelliteAtlas;

                foreach (IZoomScalingLayer zoomScalingLayer in components.ZoomScalingLayers)
                    zoomScalingLayers.Add(zoomScalingLayer);

                foreach (KeyValuePair<MapLayer, IMapLayerController> pair in components.Layers)
                {
                    await pair.Value.Disable(ct);
                    layers[pair.Key] = new MapLayerStatus(pair.Value);
                }

                layers[MapLayer.SatelliteAtlas].SharedActive = true;
                layers[MapLayer.SearchResults].SharedActive = true;
                layers[MapLayer.LiveEvents].SharedActive = false;
                layers[MapLayer.Category].SharedActive = false;
                layers[MapLayer.ParcelsAtlas].SharedActive = false;
            }
            catch (OperationCanceledException) { }
            catch (Exception e) { ReportHub.LogException(e, new ReportData(ReportCategory.TEXTURES)); }
        }

        public IMapCameraController RentCamera(in MapCameraInput cameraInput)
        {
            const int MIN_ZOOM = 5;
            const int MAX_ZOOM = 300;

            // Each time we open the fullscreen map, we unblock zoom for all layers
            foreach (IZoomScalingLayer layer in zoomScalingLayers!)
                layer.ZoomBlocked = false;

            // Clamp texture to the maximum size allowed, preserving aspect ratio
            Vector2Int zoomValues = cameraInput.ZoomValues;
            zoomValues.x = Mathf.Max(zoomValues.x, MIN_ZOOM);
            zoomValues.y = Mathf.Min(zoomValues.y, MAX_ZOOM);

            EnableLayers(cameraInput.ActivityOwner, cameraInput.EnabledLayers);
            IMapCameraControllerInternal mapCameraController = mapCameraPool!.Get();
            mapCameraController.Initialize(cameraInput.TextureResolution, zoomValues, cameraInput.EnabledLayers);
            mapCameraController.SetBackgroundColor(backgroundColor);
            rentedCameras.Add(mapCameraController);
            mapCameraController.OnReleasing += ReleaseCamera;

            mapCameraController.ZoomChanged += OnCameraZoomChanged;

            mapCameraController.SetPositionAndZoom(cameraInput.Position, cameraInput.Zoom);

            return mapCameraController;
        }

        private void ReleaseCamera(IMapActivityOwner owner, IMapCameraControllerInternal mapCameraController)
        {
            mapCameraController.OnReleasing -= ReleaseCamera;
            mapCameraController.ZoomChanged -= OnCameraZoomChanged;
            rentedCameras.Remove(mapCameraController);

            // Each time we close the fullscreen map, we reset the scale for all layers and block its zoom
            foreach (IZoomScalingLayer layer in zoomScalingLayers!)
            {
                layer.ResetToBaseScale();
                layer.ZoomBlocked = true;
            }

            DisableLayers(owner, mapCameraController.EnabledLayers);
            mapCameraPool!.Release(mapCameraController);
        }

        private void OnCameraZoomChanged(float baseZoom, float newZoom, int zoomLevel)
        {
            foreach (IZoomScalingLayer layer in zoomScalingLayers!)
                layer.ApplyCameraZoom(baseZoom, newZoom, zoomLevel);
        }

        public void SetSharedLayer(MapLayer mask, bool active)
        {
            foreach (MapLayer mapLayer in ALL_LAYERS)
            {
                if (!EnumUtils.HasFlag(mask, mapLayer))
                    continue;

                if (!layers!.TryGetValue(mapLayer, out var mapLayerStatus) || mapLayerStatus.ActivityOwners.Count == 0 || mapLayerStatus.SharedActive == active)
                    continue;

                mapLayerStatus.SharedActive = active;

                // Applied when the layer stops being suppressed
                if (mapLayerStatus.Suppressed)
                    continue;

                // Cancel activation/deactivation flow
                ResetCancellationSource(mapLayerStatus);

                if (active)
                    mapLayerStatus.MapLayerController.EnableAsync(mapLayerStatus.CTS!.Token).SuppressCancellationThrow().Forget();
                else
                    mapLayerStatus.MapLayerController.Disable(mapLayerStatus.CTS!.Token).SuppressCancellationThrow().Forget();
            }
        }

        public void ShowWorld(string worldName, RectInt? parcelBounds)
        {
            coordsUtils?.SetWorldBounds(parcelBounds);

            // The tiles of the world's whole map, over its terrain past its parcels.
            satelliteAtlas?.ShowWorld(worldName, parcelBounds.HasValue ? coordsUtils?.VisibleWorldBounds : null);
            SetBackgroundColor(WORLD_OCEAN_COLOR);
            SetSuppressedLayers(GENESIS_CITY_LAYERS);
        }

        public void ShowGenesisCity()
        {
            coordsUtils?.SetWorldBounds(null);
            satelliteAtlas?.ShowGenesisCity();
            SetBackgroundColor(null);
            SetSuppressedLayers(MapLayer.None);
        }

        private void SetBackgroundColor(Color? color)
        {
            backgroundColor = color;

            foreach (IMapCameraControllerInternal camera in rentedCameras)
                camera.SetBackgroundColor(color);
        }

        /// <summary>
        ///     Hides the layers of <paramref name="mask" /> whatever their owners and shared state ask for, and restores the others.
        /// </summary>
        private void SetSuppressedLayers(MapLayer mask)
        {
            foreach (MapLayer mapLayer in ALL_LAYERS)
            {
                if (!layers!.TryGetValue(mapLayer, out MapLayerStatus mapLayerStatus))
                    continue;

                bool suppressed = EnumUtils.HasFlag(mask, mapLayer);

                if (mapLayerStatus.Suppressed == suppressed)
                    continue;

                mapLayerStatus.Suppressed = suppressed;

                if (mapLayerStatus.ActivityOwners.Count == 0 || mapLayerStatus.SharedActive == false)
                    continue;

                ResetCancellationSource(mapLayerStatus);

                if (suppressed)
                    mapLayerStatus.MapLayerController.Disable(mapLayerStatus.CTS!.Token).SuppressCancellationThrow().Forget();
                else
                    mapLayerStatus.MapLayerController.EnableAsync(mapLayerStatus.CTS!.Token).SuppressCancellationThrow().Forget();
            }
        }

        public void CreateSystems(ref ArchSystemsWorldBuilder<World> builder)
        {
            foreach (MapLayerStatus mapLayerStatus in layers!.Values) { mapLayerStatus.MapLayerController.CreateSystems(ref builder); }
        }

        private void EnableLayers(IMapActivityOwner owner, MapLayer mask)
        {
            foreach (MapLayer mapLayer in ALL_LAYERS)
            {
                if (!EnumUtils.HasFlag(mask, mapLayer) || !layers!.TryGetValue(mapLayer, out MapLayerStatus mapLayerStatus)) continue;

                if (owner.LayersParameters.TryGetValue(mapLayer, out IMapLayerParameter parameter))
                    mapLayerStatus.MapLayerController.SetParameter(parameter);

                if (mapLayerStatus.ActivityOwners.Count == 0 && mapLayerStatus.SharedActive != false && !mapLayerStatus.Suppressed)
                {
                    // Cancel deactivation flow
                    ResetCancellationSource(mapLayerStatus);
                    mapLayerStatus.MapLayerController.EnableAsync(mapLayerStatus.CTS!.Token).SuppressCancellationThrow().Forget();
                }

                mapLayerStatus.ActivityOwners.Add(owner);
            }
        }

        private void DisableLayers(IMapActivityOwner owner, MapLayer mask)
        {
            foreach (MapLayer mapLayer in ALL_LAYERS)
            {
                if (!EnumUtils.HasFlag(mask, mapLayer) || !layers!.TryGetValue(mapLayer, out MapLayerStatus mapLayerStatus)) continue;

                if (mapLayerStatus.ActivityOwners.Contains(owner))
                    mapLayerStatus.ActivityOwners.Remove(owner);

                if (mapLayerStatus.ActivityOwners.Count == 0)
                {
                    // Cancel activation flow
                    ResetCancellationSource(mapLayerStatus);
                    mapLayerStatus.MapLayerController.Disable(mapLayerStatus.CTS!.Token).SuppressCancellationThrow().Forget();
                }
                else
                {
                    IMapActivityOwner currentOwner = mapLayerStatus.ActivityOwners[^1];
                    IReadOnlyDictionary<MapLayer, IMapLayerParameter> parametersByLayer = currentOwner.LayersParameters;

                    if (parametersByLayer.TryGetValue(mapLayer, out IMapLayerParameter? layerParam))
                        mapLayerStatus.MapLayerController.SetParameter(layerParam);
                }
            }
        }

        private void ResetCancellationSource(MapLayerStatus mapLayerStatus)
        {
            if (mapLayerStatus.CTS != null)
            {
                mapLayerStatus.CTS.Cancel();
                mapLayerStatus.CTS.Dispose();
            }

            mapLayerStatus.CTS = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        }

        private class MapLayerStatus
        {
            public readonly IMapLayerController MapLayerController;
            public readonly List<IMapActivityOwner> ActivityOwners = new ();

            public bool? SharedActive;
            public bool Suppressed;
            public CancellationTokenSource? CTS;

            public MapLayerStatus(IMapLayerController mapLayerController)
            {
                MapLayerController = mapLayerController;
            }
        }
    }
}
