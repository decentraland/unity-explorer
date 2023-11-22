using DCL.UI;
using DCLServices.MapRenderer;
using DCLServices.MapRenderer.ConsumerUtils;
using DCLServices.MapRenderer.MapCameraController;
using DCLServices.MapRenderer.MapLayers;
using System;
using UnityEngine;

namespace DCL.Navmap
{
    public class SatelliteController : ISection
    {
        private readonly SatelliteView view;
        private readonly MapCameraDragBehavior.MapCameraDragBehaviorData mapCameraDragBehaviorData;
        private readonly RectTransform rectTransform;
        private readonly IMapCameraController cameraController;
        private readonly IMapRenderer mapRenderer;

        public SatelliteController(
            SatelliteView view,
            MapCameraDragBehavior.MapCameraDragBehaviorData mapCameraDragBehaviorData,
            IMapCameraController cameraController,
            IMapRenderer mapRenderer)
        {
            this.view = view;
            this.mapCameraDragBehaviorData = mapCameraDragBehaviorData;
            this.cameraController = cameraController;
            this.mapRenderer = mapRenderer;

            rectTransform = view.GetComponent<RectTransform>();
            view.SatelliteRenderImage.EmbedMapCameraDragBehavior(mapCameraDragBehaviorData);
            view.SatelliteRenderImage.texture = cameraController.GetRenderTexture();
        }

        public void Activate()
        {
            mapRenderer.SetSharedLayer(MapLayer.SatelliteAtlas, true);
            view.SatellitePixelPerfectMapRendererTextureProvider.Activate(cameraController);
            view.gameObject.SetActive(true);
        }

        public void Deactivate()
        {
            mapRenderer.SetSharedLayer(MapLayer.SatelliteAtlas, false);
            view.SatellitePixelPerfectMapRendererTextureProvider.Deactivate();
            view.gameObject.SetActive(false);
        }

        public RectTransform GetRectTransform() =>
            rectTransform;
    }
}
