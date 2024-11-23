using DCL.MapRenderer;
using DCL.MapRenderer.MapLayers;
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
        private CategoryToggleView currentActiveToggle;

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

            navmapBus.OnPlaceSearched += OnPlaceSearched;
        }

        private void OnPlaceSearched(INavmapBus.SearchPlaceParams searchparams, IReadOnlyList<PlacesData.PlaceInfo> places, int totalresultcount)
        {
            if (string.IsNullOrEmpty(searchparams.category) && currentActiveToggle != null)
            {
                currentActiveToggle.SetVisualStatus(false);
                currentActiveToggle.Toggle.SetIsOnWithoutNotify(false);
            }
        }

        private void OnCategoryToggleChanged(MapLayer mapLayer, bool isOn, CategoryToggleView toggleView)
        {
            if (isOn)
                currentActiveToggle = toggleView;

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
