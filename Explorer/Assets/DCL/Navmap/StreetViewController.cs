using DCL.UI;
using DCLServices.MapRenderer;
using DCLServices.MapRenderer.ConsumerUtils;
using DCLServices.MapRenderer.MapCameraController;
using DCLServices.MapRenderer.MapLayers;
using System;
using UnityEngine;

namespace DCL.Navmap
{
    public class StreetViewController : ISection
    {
        private readonly StreetViewView view;
        private readonly RectTransform rectTransform;
        private readonly MapCameraDragBehavior.MapCameraDragBehaviorData mapCameraDragBehaviorData;
        private readonly IMapCameraController cameraController;
        private readonly IMapRenderer mapRenderer;

        public StreetViewController(StreetViewView view,
            MapCameraDragBehavior.MapCameraDragBehaviorData mapCameraDragBehaviorData,
            IMapCameraController cameraController,
            IMapRenderer mapRenderer)
        {
            this.view = view;
            this.mapCameraDragBehaviorData = mapCameraDragBehaviorData;
            this.cameraController = cameraController;
            this.mapRenderer = mapRenderer;

            rectTransform = view.GetComponent<RectTransform>();;
            view.StreetViewRenderImage.EmbedMapCameraDragBehavior(mapCameraDragBehaviorData);
            view.StreetViewRenderImage.texture = cameraController.GetRenderTexture();
        }

        public void Activate()
        {
            mapRenderer.SetSharedLayer(MapLayer.ParcelsAtlas, true);
            view.gameObject.SetActive(true);
            view.StreetViewPixelPerfectMapRendererTextureProvider.Activate(cameraController);
        }

        public void Deactivate()
        {
            mapRenderer.SetSharedLayer(MapLayer.ParcelsAtlas, false);
            view.StreetViewPixelPerfectMapRendererTextureProvider.Deactivate();
            view.gameObject.SetActive(false);
        }

        public RectTransform GetRectTransform() =>
            rectTransform;
    }
}
