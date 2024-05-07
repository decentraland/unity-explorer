
using DCL.Browser;
using DCL.MapRenderer;
using DCL.MapRenderer.MapLayers;
using System;

namespace DCL.Navmap
{
    public class NavmapFilterController
    {
        private const string DAO_URL = "https://decentraland.org/dao/";
        private readonly NavmapFilterView filterView;
        private readonly IMapRenderer mapRenderer;

        public NavmapFilterController(NavmapFilterView filterView, IMapRenderer mapRenderer, IWebBrowser webBrowser)
        {
            this.filterView = filterView;
            this.mapRenderer = mapRenderer;
            filterView.infoButton.onClick.AddListener(ToggleInfoContent);
            filterView.OnFilterChanged += ToggleLayer;
            filterView.daoButton.onClick.AddListener(()=>webBrowser.OpenUrl(DAO_URL));
        }

        public void SetFilterContentStatus(bool isActive) =>
            filterView.FilterContent.SetActive(isActive);

        private void ToggleInfoContent()
        {
            SetFilterContentStatus(!filterView.infoContent.activeSelf);
        }

        private void ToggleLayer(MapLayer layerName, bool isActive) =>
            mapRenderer.SetSharedLayer(layerName, isActive);
    }
}
