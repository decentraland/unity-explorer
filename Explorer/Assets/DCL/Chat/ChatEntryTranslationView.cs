using System;
using DCL.Translation.Models;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Chat
{
    public class ChatEntryTranslationView : MonoBehaviour
    {
        public Button ButtonTranslate;
        public Button ButtonSeeOriginal;
        public RectTransform LoadingSpinnerContainer;
        public Image LoadingSpinnerImage;
        public GameObject TranslationFailedContainer;

        public event Action OnTranslateClicked;
        public event Action OnSeeOriginalClicked;

        private Tween translatingAnimation;

        private void Awake()
        {
            ButtonTranslate.onClick.AddListener(() => OnTranslateClicked?.Invoke());
            ButtonSeeOriginal.onClick.AddListener(() => OnSeeOriginalClicked?.Invoke());
            translatingAnimation?.Kill();
        }

        private void OnDestroy()
        {
            translatingAnimation?.Kill();
        }

        public void SetState(TranslationState state)
        {
            translatingAnimation?.Kill();
            LoadingSpinnerImage.fillAmount = 0;
            LoadingSpinnerImage.fillClockwise = true;
            
            // Reset all states first
            ButtonTranslate.gameObject.SetActive(false);
            ButtonSeeOriginal.gameObject.SetActive(false);
            LoadingSpinnerContainer.gameObject.SetActive(false);
            TranslationFailedContainer.SetActive(false);

            switch (state)
            {
                case TranslationState.Original:
                    ButtonTranslate.gameObject.SetActive(true);
                    break;
                case TranslationState.Failed:
                    ButtonTranslate.gameObject.SetActive(true);
                    TranslationFailedContainer.SetActive(true);
                    break;

                case TranslationState.Pending:
                    LoadingSpinnerContainer.gameObject.SetActive(true);
                    translatingAnimation = DOTween.Sequence()
                        .Append(LoadingSpinnerImage.DOFillAmount(1f, 0.8f).SetEase(Ease.InOutCubic))
                        .AppendCallback(() => { LoadingSpinnerImage.fillClockwise = false; })
                        .Append(LoadingSpinnerImage.DOFillAmount(0f, 0.8f).SetEase(Ease.InOutCubic))
                        .AppendCallback(() => { LoadingSpinnerImage.fillClockwise = true; })
                        .SetLoops(-1, LoopType.Restart);
                    break;

                case TranslationState.Success:
                    ButtonSeeOriginal.gameObject.SetActive(true);
                    break;
            }
        }
    }
}