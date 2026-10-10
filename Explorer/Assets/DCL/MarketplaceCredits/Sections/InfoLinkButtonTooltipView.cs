using DG.Tweening;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.MarketplaceCredits.Sections
{
    public class InfoLinkButtonTooltipView : MonoBehaviour
    {
        [SerializeField] private CanvasGroup canvasGroup = null!;
        [SerializeField] private Button learnMoreButton = null!;
        [SerializeField] private Button backgroundCloseButton = null!;
        [SerializeField] private float fadeTime = 0.3f;

        public event Action? OnLearnMoreClicked;

        private void Awake()
        {
            Hide(true);

            learnMoreButton.onClick.AddListener(() => OnLearnMoreClicked?.Invoke());
            backgroundCloseButton.onClick.AddListener(() => Hide());
        }

        private void OnDisable() =>
            Hide(true);

        public void Show() =>
            canvasGroup.DOFade(1f, fadeTime).OnComplete(() =>
            {
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
            });

        private void Hide(bool instant = false)
        {
            if (instant)
            {
                canvasGroup.alpha = 0;
                canvasGroup.blocksRaycasts = false;
                canvasGroup.interactable = false;

                return;
            }

            canvasGroup.DOFade(0f, fadeTime)
                       .OnComplete(() =>
                        {
                            canvasGroup.interactable = false;
                            canvasGroup.blocksRaycasts = false;
                        });
        }
    }
}
