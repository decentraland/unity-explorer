using DCL.Browser;
using DCL.MapRenderer;
using DCL.MapRenderer.ConsumerUtils;
using DCL.MapRenderer.MapCameraController;
using DCL.MapRenderer.MapLayers;
using DCL.UI;
using UnityEngine;

namespace DCL.Navmap
{
    public class SatelliteController : ISection
    {
        private const string GENESIS_CITY_LINK = "https://genesis.city/";

        private readonly SatelliteView view;
        private readonly MapCameraDragBehavior.MapCameraDragBehaviorData mapCameraDragBehaviorData;
        private readonly RectTransform rectTransform;
        private readonly IMapRenderer mapRenderer;
        private readonly IWebBrowser webBrowser;

        private IMapCameraController cameraController;

        public SatelliteController(
            SatelliteView view,
            MapCameraDragBehavior.MapCameraDragBehaviorData mapCameraDragBehaviorData,
            IMapRenderer mapRenderer,
            IWebBrowser webBrowser)
        {
            this.view = view;
            this.mapCameraDragBehaviorData = mapCameraDragBehaviorData;
            this.mapRenderer = mapRenderer;
            this.webBrowser = webBrowser;

            rectTransform = view.GetComponent<RectTransform>();
            view.SatelliteRenderImage.EmbedMapCameraDragBehavior(mapCameraDragBehaviorData);
            view.OnClickedGenesisCityLink += OnClickedGenesisCityLink;
        }

        private void OnClickedGenesisCityLink()
        {
            webBrowser.OpenUrl(GENESIS_CITY_LINK);
        }

        public void InjectCameraController(IMapCameraController controller)
        {
            this.cameraController = controller;
        }

        public void Activate()
        {
            mapRenderer.SetSharedLayer(MapLayer.SatelliteAtlas, true);
            view.SatelliteRenderImage.texture = cameraController.GetRenderTexture();
            view.SatellitePixelPerfectMapRendererTextureProvider.Activate(cameraController);
            view.SatelliteRenderImage.Activate(null, cameraController.GetRenderTexture(), cameraController);
            view.gameObject.SetActive(true);
        }

        public void Deactivate()
        {
            mapRenderer.SetSharedLayer(MapLayer.SatelliteAtlas, false);
            view.SatellitePixelPerfectMapRendererTextureProvider.Deactivate();
            view.SatelliteRenderImage.Deactivate();
            view.gameObject.SetActive(false);
        }

        public RectTransform GetRectTransform() =>
            rectTransform;
    }
}
