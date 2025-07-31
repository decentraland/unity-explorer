using DG.Tweening;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Chat.ChatMessages
{
    public class ChatMessageFeedScrollButtonView : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Button scrollToBottomButton;

        [SerializeField] private TMP_Text unreadCountText;
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("Animation Settings")]
        [SerializeField] private float timeBeforeHiding = 2.0f;

        [SerializeField] private float fadeOutDuration = 0.5f;

        public event Action OnClicked;

        private void Awake()
        {
            scrollToBottomButton.onClick.AddListener(() => OnClicked?.Invoke());
        }

        private void OnDestroy()
        {
            scrollToBottomButton.onClick.RemoveAllListeners();
            canvasGroup.DOKill();
        }

        public void SetVisibility(bool isVisible, int unreadCount, bool useAnimation)
        {
            canvasGroup.DOKill();

            if (isVisible)
            {
                unreadCountText.text = unreadCount > 9 ? "+9" : unreadCount.ToString();
                canvasGroup.alpha = 1.0f;
                gameObject.SetActive(true);
            }
            else
            {
                if (useAnimation && gameObject.activeInHierarchy)
                {
                    canvasGroup.DOFade(0.0f, fadeOutDuration)
                        .SetDelay(timeBeforeHiding)
                        .OnComplete(() => gameObject.SetActive(false));
                }
                else
                {
                    canvasGroup.alpha = 0.0f;
                    gameObject.SetActive(false);
                }
            }
        }

        public void StartFocusFade(float targetAlpha, float duration, Ease easing)
        {
            canvasGroup.DOKill();
            canvasGroup.DOFade(targetAlpha, duration).SetEase(easing);
        }
    }
}