using DCL.VoiceChat.CommunityVoiceChat;
using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DCL.VoiceChat
{
    public class VoiceChatPanelView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        public event Action? PointerEnter;
        public event Action? PointerExit;
        public event Action? PointerClick;

        [field: SerializeField] public VoiceChatView VoiceChatView { get; private set; } = null!;
        [field: SerializeField] public VoiceChatPanelResizeView VoiceChatPanelResizeView { get; private set; } = null!;
        [field: SerializeField] public CommunityVoiceChatTitlebarView CommunityVoiceChatView { get; private set; } = null!;
        [field: SerializeField] public SceneVoiceChatTitlebarView SceneVoiceChatTitlebarView { get; private set; } = null!;

        public void OnPointerEnter(PointerEventData eventData)
        {
            PointerEnter?.Invoke();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            PointerExit?.Invoke();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            PointerClick?.Invoke();
        }
    }
}
