using System;
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
    }
}