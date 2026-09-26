using DCL.MapRenderer;
using DCL.MapRenderer.MapLayers;
using DG.Tweening;
using System.Collections.Generic;

namespace DCL.Navmap.FilterPanel
{
    public class NavmapFilterPanelController
    {
        private const float ANIMATION_DURATION = 0.2f;
        private readonly IMapRenderer mapRenderer;
        private readonly NavmapFilterPanelView view;
        private bool isToggled;
        private readonly HashSet<MapLayer> currentActiveLayers;

        public NavmapFilterPanelController(IMapRenderer mapRenderer, NavmapFilterPanelView view)
        {
            this.mapRenderer = mapRenderer;
            this.view = view;
            this.view.OnFilterChanged += OnFilterChanged;
            view.canvasGroup.alpha = 0;
            view.canvasGroup.blocksRaycasts = false;
            view.canvasGroup.interactable = false;

            // Set the default active layers
            currentActiveLayers = new HashSet<MapLayer>
            {
                MapLayer.LiveEvents,
                MapLayer.ScenesOfInterest,
                MapLayer.Pins,
                MapLayer.HotUsersMarkers,
                MapLayer.SatelliteAtlas,
            };
        }

        private void OnFilterChanged(MapLayer layer, bool isActive)
        {
            mapRenderer.SetSharedLayer(layer, isActive);

            if (isActive)
                currentActiveLayers.Add(layer);
            else
                currentActiveLayers.Remove(layer);
        }

        public void ToggleFilterPanel()
        {
            isToggled = !isToggled;
            view.ToggleFilterPanel(isToggled);
            view.canvasGroup.DOFade(isToggled ? 1 : 0, ANIMATION_DURATION).SetEase(Ease.Linear);
        }

        public bool IsFilterActivated(MapLayer layer) =>
            currentActiveLayers.Contains(layer);
    }
}
