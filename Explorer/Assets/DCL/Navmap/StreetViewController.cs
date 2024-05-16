using DCL.Character.CharacterMotion.Components;
using DCL.MapRenderer;
using DCL.MapRenderer.ConsumerUtils;
using DCL.MapRenderer.MapCameraController;
using DCL.MapRenderer.MapLayers;
using DCL.UI;
using System;
using UnityEngine;

namespace DCL.Navmap
{
    public class StreetViewController : ISection
    {
        private readonly StreetViewView view;
        private readonly RectTransform rectTransform;
        private readonly MapCameraDragBehavior.MapCameraDragBehaviorData mapCameraDragBehaviorData;
        private readonly IMapRenderer mapRenderer;

        private IMapCameraController cameraController;

        public StreetViewController(StreetViewView view,
            MapCameraDragBehavior.MapCameraDragBehaviorData mapCameraDragBehaviorData,
            IMapRenderer mapRenderer)
        {
            this.view = view;
            this.mapCameraDragBehaviorData = mapCameraDragBehaviorData;
            this.mapRenderer = mapRenderer;

            rectTransform = view.GetComponent<RectTransform>();;
            view.StreetViewRenderImage.EmbedMapCameraDragBehavior(mapCameraDragBehaviorData);
        }

        public void InjectCameraController(IMapCameraController controller)
        {
            this.cameraController = controller;
        }

        public void Activate()
        {
            mapRenderer.SetSharedLayer(MapLayer.SatelliteAtlas, false);
            mapRenderer.SetSharedLayer(MapLayer.ParcelsAtlas, true);
            view.gameObject.SetActive(true);
            view.StreetViewPixelPerfectMapRendererTextureProvider.Activate(cameraController);
            view.StreetViewRenderImage.texture = cameraController.GetRenderTexture();
            view.StreetViewRenderImage.Activate(null, cameraController.GetRenderTexture(), cameraController);
        }

        public void Deactivate()
        {
            mapRenderer.SetSharedLayer(MapLayer.ParcelsAtlas, false);
            view.StreetViewPixelPerfectMapRendererTextureProvider.Deactivate();
            view.StreetViewRenderImage.Deactivate();
            view.gameObject.SetActive(false);
        }

        public void Animate(int triggerId)
        {
            view.gameObject.SetActive(triggerId == AnimationHashes.IN);
        }

        public void ResetAnimator() { }

        public RectTransform GetRectTransform() =>
            rectTransform;
    }
}
