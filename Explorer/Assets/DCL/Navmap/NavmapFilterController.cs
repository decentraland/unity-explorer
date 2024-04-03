
using DCL.MapRenderer;
using DCL.MapRenderer.MapLayers;
using System;

namespace DCL.Navmap
{
    public class NavmapFilterController
    {
        private readonly NavmapFilterView filterView;
        private readonly IMapRenderer mapRenderer;

        public NavmapFilterController(NavmapFilterView filterView, IMapRenderer mapRenderer)
        {
            this.filterView = filterView;
            this.mapRenderer = mapRenderer;
            filterView.infoButton.onClick.AddListener(ToggleInfoContent);
            filterView.OnFilterChanged += ToggleLayer;
        }

        private void ToggleInfoContent()
        {
            filterView.infoContent.SetActive(!filterView.infoContent.activeSelf);
        }

        private void ToggleLayer(MapLayer layerName, bool isActive) =>
            mapRenderer.SetSharedLayer(layerName, isActive);
    }
}
