using DCL.MapRenderer.CoordsUtils;
using DCL.MapRenderer.Culling;
using DCL.MapRenderer.MapCameraController;
using DCL.MapRenderer.MapLayers;
using DCL.MapRenderer.MapLayers.SatelliteAtlas;
using System.Collections.Generic;
using UnityEngine.Pool;

namespace DCL.MapRenderer.ComponentsFactory
{
    internal readonly struct MapRendererComponents
    {
        public readonly MapRendererConfiguration ConfigurationInstance;
        public readonly IReadOnlyDictionary<MapLayer, IMapLayerController> Layers;
        public readonly IReadOnlyList<IZoomScalingLayer> ZoomScalingLayers;
        public readonly IMapCullingController CullingController;
        public readonly IObjectPool<IMapCameraControllerInternal> MapCameraControllers;

        /// <summary>Switched by the map renderer between Genesis City and a world; without them the map stays on Genesis City.</summary>
        public readonly ICoordsUtils? CoordsUtils;
        public readonly SatelliteChunkAtlasController? SatelliteAtlas;

        public MapRendererComponents(MapRendererConfiguration configurationInstance, IReadOnlyDictionary<MapLayer, IMapLayerController> layers,
            IReadOnlyList<IZoomScalingLayer> zoomScalingLayers, IMapCullingController cullingController, IObjectPool<IMapCameraControllerInternal> mapCameraControllers,
            ICoordsUtils? coordsUtils = null, SatelliteChunkAtlasController? satelliteAtlas = null)
        {
            ConfigurationInstance = configurationInstance;
            Layers = layers;
            CullingController = cullingController;
            MapCameraControllers = mapCameraControllers;
            ZoomScalingLayers = zoomScalingLayers;
            CoordsUtils = coordsUtils;
            SatelliteAtlas = satelliteAtlas;
        }
    }
}
