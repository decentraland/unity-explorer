using System;
using DG.Tweening;
using MVC;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DCL.Chat
{
    public class ChatMainView : ViewBase, IView, IPointerEnterHandler, IPointerExitHandler, IDisposable
    {
        public event Action OnPointerEnterEvent;
        public event Action OnPointerExitEvent;
        public event Action OnClickedOutsideEvent;
     
        [field: SerializeField]
        public ChatConfig Config { get; private set; }
        
        [SerializeField]
        private CanvasGroup sharedBackgroundCanvasGroup;
        
        [field: SerializeField]
        public ChatChannelsView ConversationToolbarView2 { get; private set; }

        [field: SerializeField]
        public ChatMessageFeedView MessageFeedView { get; private set; }

        [field: SerializeField]
        public ChatInputView InputView { get; private set; }

        [field: SerializeField]
        public ChatTitleBarView2 TitlebarView { get; private set; }

        [field: SerializeField]
        public ChatMemberListView MemberListView { get; private set; }

        public void Dispose()
        {
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            OnPointerEnterEvent?.Invoke();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            OnPointerExitEvent?.Invoke();
        }
        
        public void SetSharedBackgroundFocusState(bool isFocused, bool animate, float duration, Ease easing)
        {
            // This is the logic that was previously in ChatMessageFeedView, now in its correct home.
            sharedBackgroundCanvasGroup.DOKill();

            float targetAlpha = isFocused ? 1.0f : 0.0f;
            float fadeDuration = animate ? duration : 0f;

            if (isFocused && !sharedBackgroundCanvasGroup.gameObject.activeSelf)
                sharedBackgroundCanvasGroup.gameObject.SetActive(true);

            sharedBackgroundCanvasGroup.DOFade(targetAlpha, fadeDuration)
                .SetEase(easing)
                .OnComplete(() =>
                {
                    if (!isFocused)
                    {
                        sharedBackgroundCanvasGroup.gameObject.SetActive(false);
                    }
                });
        }
    }
}