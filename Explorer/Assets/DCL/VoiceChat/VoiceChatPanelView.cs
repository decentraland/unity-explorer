using DCL.VoiceChat.CommunityVoiceChat;
using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DCL.VoiceChat
{
    public class VoiceChatPanelView : MonoBehaviour, IPointerClickHandler
    {
        public event Action? PointerClick;
        public event Action? PointerEnterChatArea;
        public event Action? PointerExitChatArea;
        public event Action? PointerClickChatArea;

        [field: SerializeField] public VoiceChatView VoiceChatView { get; private set; } = null!;
        [field: SerializeField] public VoiceChatPanelResizeView VoiceChatPanelResizeView { get; private set; } = null!;
        [field: SerializeField] public CommunityVoiceChatTitlebarView CommunityVoiceChatView { get; private set; } = null!;
        [field: SerializeField] public SceneVoiceChatTitlebarView SceneVoiceChatTitlebarView { get; private set; } = null!;

        public void OnPointerClick(PointerEventData eventData)
        {
            PointerClick?.Invoke();
        }

        public void ChatAreaOnPointerEnter()
        {
            PointerEnterChatArea?.Invoke();
        }

        public void ChatAreaOnPointerExit()
        {
            PointerExitChatArea?.Invoke();
        }

        public void ChatAreaOnPointerClick()
        {
            PointerClickChatArea?.Invoke();
        }
    }
}
