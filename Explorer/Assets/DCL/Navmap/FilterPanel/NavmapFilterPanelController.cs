using DCL.MapRenderer;
using DCL.MapRenderer.MapLayers;

namespace DCL.Navmap.FilterPanel
{
    public class NavmapFilterPanelController
    {
        private readonly IMapRenderer mapRenderer;
        private readonly NavmapFilterPanelView view;

        public NavmapFilterPanelController(IMapRenderer mapRenderer, NavmapFilterPanelView view)
        {
            this.mapRenderer = mapRenderer;
            this.view = view;
            this.view.OnFilterChanged += OnFilterChanged;
        }

        private void OnFilterChanged(MapLayer layer, bool isActive) =>
            mapRenderer.SetSharedLayer(layer, isActive);
    }
}
