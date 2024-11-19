using Arch.Core;
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.MapRenderer.CommonBehavior;
using DCL.MapRenderer.ComponentsFactory;
using DCL.MapRenderer.Culling;
using DCL.MapRenderer.MapCameraController;
using DCL.MapRenderer.MapLayers;
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
        private static readonly MapLayer[] ALL_LAYERS = EnumUtils.Values<MapLayer>();

        private readonly IMapRendererComponentsFactory componentsFactory;

        private CancellationToken cancellationToken;

        private Dictionary<MapLayer, MapLayerStatus> layers;
        private List<IZoomScalingLayer> zoomScalingLayers;

        private MapRendererConfiguration configurationInstance;

        private IObjectPool<IMapCameraControllerInternal> mapCameraPool;

        internal IMapCullingController cullingController { get; private set; }

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
                cullingController = components.CullingController;
                mapCameraPool = components.MapCameraControllers;
                configurationInstance = components.ConfigurationInstance;

                foreach (IZoomScalingLayer zoomScalingLayer in components.ZoomScalingLayers)
                    zoomScalingLayers.Add(zoomScalingLayer);

                foreach (KeyValuePair<MapLayer, IMapLayerController> pair in components.Layers)
                {
                    pair.Value.Disable(ct);
                    layers[pair.Key] = new MapLayerStatus(pair.Value);
                }

                layers[MapLayer.SatelliteAtlas].SharedActive = true;
                layers[MapLayer.Art].SharedActive = false;
                layers[MapLayer.Business].SharedActive = false;
                layers[MapLayer.Education].SharedActive = false;
                layers[MapLayer.Casino].SharedActive = false;
                layers[MapLayer.Crypto].SharedActive = false;
                layers[MapLayer.Fashion].SharedActive = false;
                layers[MapLayer.Game].SharedActive = false;
                layers[MapLayer.Music].SharedActive = false;
                layers[MapLayer.Shop].SharedActive = false;
                layers[MapLayer.Social].SharedActive = false;
                layers[MapLayer.Sports].SharedActive = false;
            }
            catch (OperationCanceledException)
            {
                // just ignore
            }
            catch (Exception e) { ReportHub.LogException(e, new ReportData(ReportCategory.TEXTURES)); }
        }

        public IMapCameraController RentCamera(in MapCameraInput cameraInput)
        {
            const int MIN_ZOOM = 5;
            const int MAX_ZOOM = 300;

            // Clamp texture to the maximum size allowed, preserving aspect ratio
            Vector2Int zoomValues = cameraInput.ZoomValues;
            zoomValues.x = Mathf.Max(zoomValues.x, MIN_ZOOM);
            zoomValues.y = Mathf.Min(zoomValues.y, MAX_ZOOM);

            EnableLayers(cameraInput.ActivityOwner, cameraInput.EnabledLayers);
            IMapCameraControllerInternal mapCameraController = mapCameraPool.Get();
            mapCameraController.Initialize(cameraInput.TextureResolution, zoomValues, cameraInput.EnabledLayers);
            mapCameraController.OnReleasing += ReleaseCamera;

            mapCameraController.ZoomChanged += OnCameraZoomChanged;

            mapCameraController.SetPositionAndZoom(cameraInput.Position, cameraInput.Zoom);

            return mapCameraController;
        }

        private void ReleaseCamera(IMapActivityOwner owner, IMapCameraControllerInternal mapCameraController)
        {
            mapCameraController.OnReleasing -= ReleaseCamera;
            mapCameraController.ZoomChanged -= OnCameraZoomChanged;

            foreach (IZoomScalingLayer layer in zoomScalingLayers)
                layer.ResetToBaseScale();

            DisableLayers(owner, mapCameraController.EnabledLayers);
            mapCameraPool.Release(mapCameraController);
        }

        private void OnCameraZoomChanged(float baseZoom, float newZoom, int zoomLevel)
        {
            foreach (IZoomScalingLayer layer in zoomScalingLayers)
                layer.ApplyCameraZoom(baseZoom, newZoom, zoomLevel);
        }

        public void SetSharedLayer(MapLayer mask, bool active)
        {
            foreach (MapLayer mapLayer in ALL_LAYERS)
            {
                if (!EnumUtils.HasFlag(mask, mapLayer) || !layers.TryGetValue(mapLayer, out MapLayerStatus mapLayerStatus) || mapLayerStatus.ActivityOwners.Count == 0 || mapLayerStatus.SharedActive == active)
                    continue;

                mapLayerStatus.SharedActive = active;

                // Cancel activation/deactivation flow
                ResetCancellationSource(mapLayerStatus);

                if (active)
                    mapLayerStatus.MapLayerController.Enable(mapLayerStatus.CTS.Token).SuppressCancellationThrow().Forget();
                else
                    mapLayerStatus.MapLayerController.Disable(mapLayerStatus.CTS.Token).SuppressCancellationThrow().Forget();
            }
        }

        public void CreateSystems(ref ArchSystemsWorldBuilder<World> builder)
        {
            foreach (MapLayerStatus mapLayerStatus in layers.Values) { mapLayerStatus.MapLayerController.CreateSystems(ref builder); }
        }

        private void EnableLayers(IMapActivityOwner owner, MapLayer mask)
        {
            foreach (MapLayer mapLayer in ALL_LAYERS)
            {
                if (!EnumUtils.HasFlag(mask, mapLayer) || !layers.TryGetValue(mapLayer, out MapLayerStatus mapLayerStatus)) continue;

                if (owner.LayersParameters.TryGetValue(mapLayer, out IMapLayerParameter parameter))
                    mapLayerStatus.MapLayerController.SetParameter(parameter);

                if (mapLayerStatus.ActivityOwners.Count == 0 && mapLayerStatus.SharedActive != false)
                {
                    // Cancel deactivation flow
                    ResetCancellationSource(mapLayerStatus);
                    mapLayerStatus.MapLayerController.Enable(mapLayerStatus.CTS.Token).SuppressCancellationThrow().Forget();
                }

                mapLayerStatus.ActivityOwners.Add(owner);
            }
        }

        private void DisableLayers(IMapActivityOwner owner, MapLayer mask)
        {
            foreach (MapLayer mapLayer in ALL_LAYERS)
            {
                if (!EnumUtils.HasFlag(mask, mapLayer) || !layers.TryGetValue(mapLayer, out MapLayerStatus mapLayerStatus)) continue;

                if (mapLayerStatus.ActivityOwners.Contains(owner))
                    mapLayerStatus.ActivityOwners.Remove(owner);

                if (mapLayerStatus.ActivityOwners.Count == 0)
                {
                    // Cancel activation flow
                    ResetCancellationSource(mapLayerStatus);
                    mapLayerStatus.MapLayerController.Disable(mapLayerStatus.CTS.Token).SuppressCancellationThrow().Forget();
                }
                else
                {
                    IMapActivityOwner currentOwner = mapLayerStatus.ActivityOwners[^1];
                    IReadOnlyDictionary<MapLayer, IMapLayerParameter> parametersByLayer = currentOwner.LayersParameters;

                    if (parametersByLayer.ContainsKey(mapLayer))
                        mapLayerStatus.MapLayerController.SetParameter(parametersByLayer[mapLayer]);
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

        public void Dispose()
        {
            foreach (MapLayerStatus status in layers.Values)
            {
                if (status.CTS != null)
                {
                    status.CTS.Cancel();
                    status.CTS.Dispose();
                    status.CTS = null;
                }

                status.MapLayerController.Dispose();
            }

            cullingController?.Dispose();

            if (configurationInstance)
                UnityObjectUtils.SafeDestroy(configurationInstance.gameObject);
        }

        private class MapLayerStatus
        {
            public readonly IMapLayerController MapLayerController;
            public readonly List<IMapActivityOwner> ActivityOwners = new ();

            public bool? SharedActive;
            public CancellationTokenSource CTS;

            public MapLayerStatus(IMapLayerController mapLayerController)
            {
                MapLayerController = mapLayerController;
            }
        }
    }
}
