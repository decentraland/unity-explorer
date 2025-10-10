using UnityEngine;
using DG.Tweening;

namespace DCL.UI.Buttons
{
    [RequireComponent(typeof(HoverableButton))]
    public class ShowTooltipOnHoverElementFade : MonoBehaviour
    {
        [field: SerializeField]
        private GameObject tooltip { get; set; }

        [field: SerializeField]
        private float fadeDuration = 0.2f;

        [field: SerializeField]
        private HoverableButton hoverableButton { get; set; }

        [field: SerializeField]
        private CanvasGroup tooltipCanvasGroup { get; set; }

        private Tween activeTween;

        private void Awake()
        {
            // Subscribe to the button's hover events
            hoverableButton.OnButtonHover += ShowTooltip;
            hoverableButton.OnButtonUnhover += HideTooltip;

            // Make sure the tooltip is properly hidden at the start
            if (tooltip != null)
            {
                tooltip.SetActive(false);
                tooltipCanvasGroup.alpha = 0f;
            }
        }

        private void OnDestroy()
        {
            if (hoverableButton != null)
            {
                hoverableButton.OnButtonHover -= ShowTooltip;
                hoverableButton.OnButtonUnhover -= HideTooltip;
            }

            // Always kill tweens on destroy to prevent them running on a destroyed object
            activeTween?.Kill();
        }

        private void ShowTooltip()
        {
            if (tooltipCanvasGroup == null) return;

            // Kill any previously running tween to avoid conflicts
            activeTween?.Kill();

            tooltip.SetActive(true);
            // Start the fade-in animation
            activeTween = tooltipCanvasGroup.DOFade(1f, fadeDuration);
        }

        private void HideTooltip()
        {
            if (tooltipCanvasGroup == null) return;

            // Kill any previously running tween
            activeTween?.Kill();

            // Start the fade-out and disable the GameObject once it's invisible
            activeTween = tooltipCanvasGroup.DOFade(0f, fadeDuration)
                .OnComplete(() =>
                {
                    tooltip.SetActive(false);
                });
        }

        private void OnDisable()
        {
            // If this component's GameObject is disabled, ensure the tooltip is also hidden instantly
            if (tooltip != null && tooltip.activeSelf)
            {
                activeTween?.Kill();
                tooltip.SetActive(false);
            }
        }
    }
}