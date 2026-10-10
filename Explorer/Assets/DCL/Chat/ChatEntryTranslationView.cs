using DCL.Translation;
using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Chat
{
    public class ChatEntryTranslationView : MonoBehaviour
    {
        public Button? buttonTranslate;
        public Button? buttonSeeOriginal;
        public RectTransform? loadingSpinnerContainer;
        public Image? loadingSpinnerImage;
        public GameObject? translationFailedContainer;

        public event Action? OnTranslateClicked;
        public event Action? OnSeeOriginalClicked;

        private Tween? translatingAnimation;

        private void Awake()
        {
            buttonTranslate?.onClick.AddListener(() => OnTranslateClicked?.Invoke());
            buttonSeeOriginal?.onClick.AddListener(() => OnSeeOriginalClicked?.Invoke());
            translatingAnimation?.Kill();
        }

        private void OnDestroy()
        {
            translatingAnimation?.Kill();
        }

        public void SetState(TranslationState state)
        {
            translatingAnimation?.Kill();
            if (loadingSpinnerImage != null)
            {
                loadingSpinnerImage.fillAmount = 0;
                loadingSpinnerImage.fillClockwise = true;

                // Reset all states first
                buttonTranslate?.gameObject.SetActive(false);
                buttonSeeOriginal?.gameObject.SetActive(false);
                loadingSpinnerContainer?.gameObject.SetActive(false);
                translationFailedContainer?.SetActive(false);

                switch (state)
                {
                    case TranslationState.Original:
                        buttonTranslate?.gameObject.SetActive(true);
                        break;
                    case TranslationState.Failed:
                        buttonTranslate?.gameObject.SetActive(true);
                        translationFailedContainer?.SetActive(true);
                        break;

                    case TranslationState.Pending:
                        loadingSpinnerContainer?.gameObject.SetActive(true);
                        translatingAnimation = DOTween.Sequence()
                            .Append(loadingSpinnerImage.DOFillAmount(1f, 0.8f).SetEase(Ease.InOutCubic))
                            .AppendCallback(() => { loadingSpinnerImage.fillClockwise = false; })
                            .Append(loadingSpinnerImage.DOFillAmount(0f, 0.8f).SetEase(Ease.InOutCubic))
                            .AppendCallback(() => { loadingSpinnerImage.fillClockwise = true; })
                            .SetLoops(-1, LoopType.Restart);
                        break;

                    case TranslationState.Success:
                        buttonSeeOriginal?.gameObject.SetActive(true);
                        break;
                }
            }
        }
    }
}