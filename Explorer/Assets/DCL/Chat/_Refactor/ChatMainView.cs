using DCL.Chat;
using DCL.VoiceChat;
using System;
using MVC;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DCL.ChatArea
{
    public class ChatMainView : ViewBase, IView, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler, IDisposable
    {
        public event Action? OnPointerEnterEvent;
        public event Action? OnPointerExitEvent;

        [field: SerializeField] public ChatPanelView ChatPanelView { get; private set; } = null!;
        [field: SerializeField] public VoiceChatPanelView VoiceChatPanelView { get; private set; } = null!;

        public void OnPointerEnter(PointerEventData eventData)
        {
            VoiceChatPanelView.ChatAreaOnPointerEnter();
            OnPointerEnterEvent?.Invoke();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            VoiceChatPanelView.ChatAreaOnPointerExit();
            OnPointerExitEvent?.Invoke();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            VoiceChatPanelView.ChatAreaOnPointerClick();
        }

        public void Dispose()
        {
        }

    }
}
