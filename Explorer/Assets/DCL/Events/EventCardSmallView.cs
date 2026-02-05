using DG.Tweening;
using UnityEngine;

namespace DCL.Events
{
    public class EventCardSmallView : EventCardView
    {
        private const float HOVER_ANIMATION_DURATION = 0.2f;
        private const float HOVER_ANIMATION_NAME_CONTAINER_Y_OFFSET = 8f;
        private const float HOVER_ANIMATION_DATE_CONTAINER_Y_OFFSET = 30f;

        [Header("Animations")]
        [SerializeField] private CanvasGroup hostCanvasGroup = null!;
        [SerializeField] private CanvasGroup actionButtonsCanvasGroup = null!;
        [SerializeField] private RectTransform nameContainer = null!;
        [SerializeField] private RectTransform dateContainer = null!;

        private Tweener? hostCanvasGroupTween;
        private Tweener? actionButtonsCanvasGroupTween;
        private Tweener? dateContainerTween;
        private Tweener? nameContainerTween;
        private Vector2 originalNameContainerAnchoredPosition;
        private Vector2 originalDateContainerAnchoredPosition;

        protected override void Awake()
        {
            base.Awake();
            originalNameContainerAnchoredPosition = nameContainer.anchoredPosition;
            originalDateContainerAnchoredPosition = dateContainer.anchoredPosition;
        }

        protected override void PlayHoverAnimation()
        {
            hostCanvasGroupTween?.Kill();
            actionButtonsCanvasGroupTween?.Kill();
            nameContainerTween?.Kill();
            dateContainerTween?.Kill();

            hostCanvasGroupTween = DOTween.To(() => hostCanvasGroup.alpha,
                                    newAlpha => hostCanvasGroup.alpha = newAlpha,
                                    0f,
                                    HOVER_ANIMATION_DURATION)
                               .SetEase(Ease.OutQuad);

            actionButtonsCanvasGroup.blocksRaycasts = true;
            actionButtonsCanvasGroupTween = DOTween.To(() => actionButtonsCanvasGroup.alpha,
                                             newAlpha => actionButtonsCanvasGroup.alpha = newAlpha,
                                             1f,
                                             HOVER_ANIMATION_DURATION)
                                        .SetEase(Ease.OutQuad);

            nameContainerTween = nameContainer.DOAnchorPos(originalNameContainerAnchoredPosition + (Vector2.up * HOVER_ANIMATION_NAME_CONTAINER_Y_OFFSET), HOVER_ANIMATION_DURATION)
                                              .SetEase(Ease.OutQuad);

            dateContainerTween = dateContainer.DOAnchorPos(originalDateContainerAnchoredPosition + (Vector2.up * HOVER_ANIMATION_DATE_CONTAINER_Y_OFFSET), HOVER_ANIMATION_DURATION)
                                              .SetEase(Ease.OutQuad);
        }

        protected override void PlayHoverExitAnimation(bool instant = false)
        {
            hostCanvasGroupTween?.Kill();
            actionButtonsCanvasGroupTween?.Kill();
            nameContainerTween?.Kill();
            dateContainerTween?.Kill();

            actionButtonsCanvasGroup.blocksRaycasts = false;

            if (instant)
            {
                hostCanvasGroup.alpha = 1f;
                actionButtonsCanvasGroup.alpha = 0f;
                nameContainer.anchoredPosition = originalNameContainerAnchoredPosition;
                dateContainer.anchoredPosition = originalDateContainerAnchoredPosition;
            }
            else
            {
                hostCanvasGroupTween = DOTween.To(() => hostCanvasGroup.alpha,
                                        newAlpha => hostCanvasGroup.alpha = newAlpha,
                                        1f,
                                        HOVER_ANIMATION_DURATION)
                                   .SetEase(Ease.OutQuad);

                actionButtonsCanvasGroupTween = DOTween.To(() => actionButtonsCanvasGroup.alpha,
                                                 newAlpha => actionButtonsCanvasGroup.alpha = newAlpha,
                                                 0f,
                                                 HOVER_ANIMATION_DURATION)
                                            .SetEase(Ease.OutQuad);

                nameContainerTween = dateContainer.DOAnchorPos(originalNameContainerAnchoredPosition, HOVER_ANIMATION_DURATION)
                                                  .SetEase(Ease.OutQuad);

                dateContainerTween = dateContainer.DOAnchorPos(originalDateContainerAnchoredPosition, HOVER_ANIMATION_DURATION)
                                                  .SetEase(Ease.OutQuad);
            }
        }
    }
}
