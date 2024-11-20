using DCL.MapRenderer;
using DCL.MapRenderer.MapLayers;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.Navmap
{
    public class CategoryFilterController : IDisposable
    {
        private readonly List<CategoryToggleView> categoryToggles;
        private readonly IMapRenderer mapRenderer;
        private readonly INavmapBus navmapBus;

        public CategoryFilterController(List<CategoryToggleView> categoryToggles, IMapRenderer mapRenderer, INavmapBus navmapBus)
        {
            this.categoryToggles = categoryToggles;
            this.mapRenderer = mapRenderer;
            this.navmapBus = navmapBus;

            foreach (CategoryToggleView categoryToggleView in this.categoryToggles)
            {
                mapRenderer.SetSharedLayer(categoryToggleView.Layer, false);
                categoryToggleView.ToggleChanged += OnCategoryToggleChanged;
            }
        }

        private void OnCategoryToggleChanged(MapLayer mapLayer, bool isOn)
        {
            navmapBus.FilterByCategory(isOn ? mapLayer.ToString() : null);
            mapRenderer.SetSharedLayer(mapLayer, isOn);
        }

        public void Dispose()
        {
            foreach (CategoryToggleView categoryToggleView in categoryToggles)
                categoryToggleView.ToggleChanged -= OnCategoryToggleChanged;
        }
    }
}
