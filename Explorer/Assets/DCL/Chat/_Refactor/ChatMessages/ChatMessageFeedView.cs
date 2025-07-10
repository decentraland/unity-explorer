using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

namespace DCL.Chat
{
    public class ChatMessageFeedView : MonoBehaviour, IChatMessageFeedView
    {
        [SerializeField]
        private CanvasGroup scrollbarCanvasGroup;

        public void SetMessages(IReadOnlyList<MessageData> messages)
        {
        }

        public void Clear()
        {
        }

        public void Show()
        {
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        public void SetFocusedState(bool isFocused, bool animate, float duration, Ease easing)
        {
            scrollbarCanvasGroup.DOKill();
            float targetAlpha = isFocused ? 1.0f : 0.0f;
            float fadeDuration = animate ? duration : 0f;

            scrollbarCanvasGroup
                .DOFade(isFocused ? 1.0f : 0.0f, fadeDuration)
                .SetEase(easing)
                .OnComplete(() =>
                {

                });
        }
    }
}