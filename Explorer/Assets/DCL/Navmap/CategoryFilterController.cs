using DCL.MapRenderer;
using DCL.MapRenderer.MapLayers;
using System;
using System.Collections.Generic;

namespace DCL.Navmap
{
    public class CategoryFilterController : IDisposable
    {
        private readonly List<CategoryToggleView> categoryToggles;
        private readonly IMapRenderer mapRenderer;

        public CategoryFilterController(List<CategoryToggleView> categoryToggles, IMapRenderer mapRenderer)
        {
            this.categoryToggles = categoryToggles;
            this.mapRenderer = mapRenderer;

            foreach (CategoryToggleView categoryToggleView in this.categoryToggles)
            {
                mapRenderer.SetSharedLayer(categoryToggleView.Layer, false);
                categoryToggleView.ToggleChanged += OnCategoryToggleChanged;
            }
        }

        private void OnCategoryToggleChanged(MapLayer mapLayer, bool isOn)
        {
            mapRenderer.SetSharedLayer(mapLayer, isOn);
        }

        public void Dispose()
        {
            foreach (CategoryToggleView categoryToggleView in categoryToggles)
                categoryToggleView.ToggleChanged -= OnCategoryToggleChanged;
        }
    }
}
