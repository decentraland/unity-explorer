using DG.Tweening;
using System;

namespace DCL.Minimap
{
    public class SceneRestrictionsController : IDisposable
    {
        private readonly SceneRestrictionsView restrictionsView;
        public SceneRestrictionsController(SceneRestrictionsView restrictionsView)
        {
            this.restrictionsView = restrictionsView;

            restrictionsView.OnPointerEnterEvent += OnMouseEnter;
            restrictionsView.OnPointerExitEvent += OnMouseExit;
        }

        public void Dispose()
        {
            restrictionsView.OnPointerEnterEvent -= OnMouseEnter;
            restrictionsView.OnPointerExitEvent -= OnMouseExit;
        }

        private void OnMouseEnter() =>
            restrictionsView.toastCanvasGroup.DOFade(1f, 0.5f);

        private void OnMouseExit() =>
            restrictionsView.toastCanvasGroup.DOFade(0f, 0.5f);
    }
}
