using DCL.VoiceChat;
using System;
using MVC;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DCL.Chat
{
    public class ChatMainView : ViewBase, IView, IPointerEnterHandler, IPointerExitHandler, IDisposable
    {
        public event Action? OnPointerEnterEvent;
        public event Action? OnPointerExitEvent;

        [field: SerializeField] public ChatPanelView ChatPanelView { get; private set; } = null!;
        [field: SerializeField] public VoiceChatPanelView VoiceChatPanelView { get; private set; } = null!;

        public void OnPointerEnter(PointerEventData eventData)
        {
            OnPointerEnterEvent?.Invoke();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            OnPointerExitEvent?.Invoke();
        }

        public void Dispose()
        {
        }
    }
}
