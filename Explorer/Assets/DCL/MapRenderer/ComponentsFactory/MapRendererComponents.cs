using DCLServices.MapRenderer.Culling;
using DCLServices.MapRenderer.MapCameraController;
using DCLServices.MapRenderer.MapLayers;
using System.Collections.Generic;
using UnityEngine.Pool;

namespace DCLServices.MapRenderer.ComponentsFactory
{
    internal readonly struct MapRendererComponents
    {
        public readonly MapRendererConfiguration ConfigurationInstance;
        public readonly IReadOnlyDictionary<MapLayer, IMapLayerController> Layers;
        public readonly IReadOnlyList<IZoomScalingLayer> ZoomScalingLayers;
        public readonly IMapCullingController CullingController;
        public readonly IObjectPool<IMapCameraControllerInternal> MapCameraControllers;

        public MapRendererComponents(MapRendererConfiguration configurationInstance, IReadOnlyDictionary<MapLayer, IMapLayerController> layers,
            IReadOnlyList<IZoomScalingLayer> zoomScalingLayers, IMapCullingController cullingController, IObjectPool<IMapCameraControllerInternal> mapCameraControllers)
        {
            ConfigurationInstance = configurationInstance;
            Layers = layers;
            CullingController = cullingController;
            MapCameraControllers = mapCameraControllers;
            ZoomScalingLayers = zoomScalingLayers;
        }
    }
}
