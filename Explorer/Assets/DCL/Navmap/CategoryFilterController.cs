using DCL.MapRenderer;
using DCL.MapRenderer.MapLayers;
using DCL.MapRenderer.MapLayers.Categories;
using DCL.PlacesAPIService;
using System;
using System.Collections.Generic;

namespace DCL.Navmap
{
    public class CategoryFilterController : IDisposable
    {
        private readonly List<CategoryToggleView> categoryToggles;
        private readonly IMapRenderer mapRenderer;
        private readonly INavmapBus navmapBus;
        private CategoryToggleView? currentActiveToggle;

        public CategoryFilterController(List<CategoryToggleView> categoryToggles, IMapRenderer mapRenderer, INavmapBus navmapBus)
        {
            this.categoryToggles = categoryToggles;
            this.mapRenderer = mapRenderer;
            this.navmapBus = navmapBus;

            foreach (CategoryToggleView categoryToggleView in this.categoryToggles)
            {
                categoryToggleView.ToggleChanged += OnCategoryToggleChanged;
            }

            navmapBus.OnClearFilter += OnClearFilter;
        }

        private void OnClearFilter()
        {
            if (currentActiveToggle != null)
            {
                currentActiveToggle.Toggle.SetIsOnWithoutNotify(false);
                currentActiveToggle.SetVisualStatus(false);
                currentActiveToggle = null;
            }
        }

        private void OnCategoryToggleChanged(CategoriesEnum mapLayer, bool isOn, CategoryToggleView? toggleView)
        {
            if (isOn)
                currentActiveToggle = toggleView;

            navmapBus.FilterByCategory(isOn ? mapLayer.ToString() : null);
            if (mapLayer is CategoriesEnum.All or CategoriesEnum.Favorites) return;
            mapRenderer.SetSharedLayer(MapLayer.Category, isOn);
        }

        public void Dispose()
        {
            foreach (CategoryToggleView categoryToggleView in categoryToggles)
                categoryToggleView.ToggleChanged -= OnCategoryToggleChanged;
        }
    }
}
