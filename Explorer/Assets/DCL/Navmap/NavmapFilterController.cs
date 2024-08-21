using DCL.Browser;
using DCL.MapRenderer;
using DCL.MapRenderer.MapLayers;
using DCL.Multiplayer.Connections.DecentralandUrls;

namespace DCL.Navmap
{
    public class NavmapFilterController
    {
        private readonly NavmapFilterView filterView;
        private readonly IMapRenderer mapRenderer;

        public NavmapFilterController(NavmapFilterView filterView, IMapRenderer mapRenderer, IWebBrowser webBrowser)
        {
            this.filterView = filterView;
            this.mapRenderer = mapRenderer;
            filterView.infoButton.onClick.AddListener(ToggleInfoContent);
            filterView.OnFilterChanged += ToggleLayer;
            filterView.daoButton.onClick.AddListener(() => webBrowser.OpenUrl(DecentralandUrl.DAO));
        }

        public void CloseFilterContent()
        {
            filterView.FilterContent.SetActive(false);
            filterView.infoContent.SetActive(false);
            filterView.CloseButtonArea.gameObject.SetActive(false);
        }

        private void ToggleInfoContent() =>
            filterView.infoContent.SetActive(!filterView.infoContent.activeSelf);

        private void ToggleLayer(MapLayer layerName, bool isActive) =>
            mapRenderer.SetSharedLayer(layerName, isActive);
    }
}
