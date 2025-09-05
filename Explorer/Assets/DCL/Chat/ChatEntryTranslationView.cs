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
        public RectTransform LoadingSpinner;

        public event Action OnTranslateClicked;
        public event Action OnSeeOriginalClicked;

        private Tween translatingAnimation;

        private void Awake()
        {
            ButtonTranslate.onClick.AddListener(() => OnTranslateClicked?.Invoke());
            ButtonSeeOriginal.onClick.AddListener(() => OnSeeOriginalClicked?.Invoke());
        }

        private void OnDestroy()
        {
            //translatingAnimation?.Kill();
        }

        public void SetState(TranslationState state)
        {
            // Reset all states first
            ButtonTranslate.gameObject.SetActive(false);
            ButtonSeeOriginal.gameObject.SetActive(false);
            LoadingSpinner.gameObject.SetActive(false);
            //translatingAnimation?.Kill();

            switch (state)
            {
                case TranslationState.Original:
                case TranslationState.Failed:
                    ButtonTranslate.gameObject.SetActive(true);
                    break;

                case TranslationState.Pending:
                    LoadingSpinner.gameObject.SetActive(true);
                    // translatingAnimation = LoadingSpinner.transform
                    //     .DORotate(new Vector3(0, 0, -360), 1f, RotateMode.FastBeyond360)
                    //     .SetEase(Ease.Linear)
                    //     .SetLoops(-1);
                    break;

                case TranslationState.Success:
                    ButtonSeeOriginal.gameObject.SetActive(true);
                    break;
            }
        }
    }
}