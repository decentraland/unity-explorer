using System;
using MVC;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DCL.Chat
{
    public class ChatMainView : ViewBase, IView, IPointerEnterHandler, IPointerExitHandler, IDisposable
    {
        [field: SerializeField]
        public ChatConversationToolbarView ConversationToolbarView { get; private set; }

        [field: SerializeField]
        public ChatMessageFeedView MessageFeedView { get; private set; }

        [field: SerializeField]
        public ChatInputView InputView { get; private set; }

        [field: SerializeField]
        public ChatTitleBarView TitlebarView { get; private set; }

        [field: SerializeField]
        public ChatMemberListView MemberListView { get; private set; }

        public void Dispose()
        {
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
        }

        public void OnPointerExit(PointerEventData eventData)
        {
        }
    }
}