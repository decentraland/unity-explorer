using DCL.MapRenderer;
using DCL.MapRenderer.MapLayers;
using DG.Tweening;

namespace DCL.Navmap.FilterPanel
{
    public class NavmapFilterPanelController
    {
        private const float ANIMATION_DURATION = 0.2f;
        private readonly IMapRenderer mapRenderer;
        private readonly NavmapFilterPanelView view;
        private bool isToggled = false;

        public NavmapFilterPanelController(IMapRenderer mapRenderer, NavmapFilterPanelView view)
        {
            this.mapRenderer = mapRenderer;
            this.view = view;
            this.view.OnFilterChanged += OnFilterChanged;
            view.canvasGroup.alpha = 0;
            view.canvasGroup.blocksRaycasts = false;
            view.canvasGroup.interactable = false;
        }

        private void OnFilterChanged(MapLayer layer, bool isActive) =>
            mapRenderer.SetSharedLayer(layer, isActive);

        public void ToggleFilterPanel()
        {
            isToggled = !isToggled;
            view.canvasGroup.DOFade(isToggled ? 1 : 0, ANIMATION_DURATION).SetEase(Ease.Linear).OnComplete(() =>
            {
                view.canvasGroup.blocksRaycasts = isToggled;
                view.canvasGroup.interactable = isToggled;
            });
        }
    }
}
