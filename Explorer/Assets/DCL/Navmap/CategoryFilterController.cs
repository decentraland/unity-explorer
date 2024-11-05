using DCL.MapRenderer;
using DCL.MapRenderer.MapLayers;
using System.Collections.Generic;

namespace DCL.Navmap
{
    public class CategoryFilterController
    {
        private readonly IMapRenderer mapRenderer;

        public CategoryFilterController(List<CategoryToggleView> categoryToggles, IMapRenderer mapRenderer)
        {
            this.mapRenderer = mapRenderer;

            foreach (CategoryToggleView categoryToggleView in categoryToggles)
            {
                categoryToggleView.ToggleChanged += OnCategoryToggleChanged;
            }
        }

        private void OnCategoryToggleChanged(MapLayer mapLayer, bool isOn)
        {
            mapRenderer.SetSharedLayer(mapLayer, isOn);
        }
    }
}
