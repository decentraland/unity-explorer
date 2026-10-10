using DCL.VoiceChat.CommunityVoiceChat;
using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DCL.VoiceChat
{
    public class VoiceChatPanelView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public event Action? PointerEnter;
        public event Action? PointerExit;

        [field: SerializeField] public PrivateVoiceChatView PrivateVoiceChatView { get; private set; } = null!;
        [field: SerializeField] public VoiceChatPanelResizeView VoiceChatPanelResizeView { get; private set; } = null!;
        [field: SerializeField] public CommunityVoiceChatPanelView CommunityVoiceChatView { get; private set; } = null!;
        [field: SerializeField] public SceneVoiceChatPanelView SceneVoiceChatPanelView { get; private set; } = null!;

        public void OnPointerEnter(PointerEventData eventData)
        {
            PointerEnter?.Invoke();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            PointerExit?.Invoke();
        }
    }
}
