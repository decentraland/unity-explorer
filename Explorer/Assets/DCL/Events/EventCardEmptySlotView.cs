using DG.Tweening;
using UnityEngine;

namespace DCL.Events
{
    public class EventCardEmptySlotView : EventCardView
    {
        private const float HOVER_ANIMATION_DURATION = 0.3f;

        [Header("Animations")]
        [SerializeField] private CanvasGroup mainCanvasGroup = null!;

        private Tweener? mainCanvasGroupTween;

        protected override void PlayHoverAnimation()
        {
            mainCanvasGroupTween?.Kill();

            mainCanvasGroup.blocksRaycasts = true;
            mainCanvasGroupTween = DOTween.To(() => mainCanvasGroup.alpha,
                                               newAlpha => mainCanvasGroup.alpha = newAlpha,
                                               1f,
                                               HOVER_ANIMATION_DURATION)
                                          .SetEase(Ease.OutQuad);
        }

        protected override void PlayHoverExitAnimation(bool instant = false)
        {
            mainCanvasGroupTween?.Kill();

            mainCanvasGroup.blocksRaycasts = false;
            mainCanvasGroupTween = DOTween.To(() => mainCanvasGroup.alpha,
                                               newAlpha => mainCanvasGroup.alpha = newAlpha,
                                               0f,
                                               HOVER_ANIMATION_DURATION)
                                          .SetEase(Ease.OutQuad);
        }
    }
}
