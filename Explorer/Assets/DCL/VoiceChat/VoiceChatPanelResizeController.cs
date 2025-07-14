using System;
using UnityEngine;

namespace DCL.VoiceChat
{
    public class VoiceChatPanelResizeController : IDisposable
    {
        private readonly VoiceChatPanelResizeView view;
        private readonly IVoiceChatState voiceChatState;

        private const float EXPANDED_COMMUNITY_VOICE_CHAT_SIZE = 300;
        private const float COLLAPSED_COMMUNITY_VOICE_CHAT_SIZE = 46;
        private const float EXPANDED_PRIVATE_VOICE_CHAT_SIZE = 100;
        private const float COLLAPSED_PRIVATE_VOICE_CHAT_SIZE = 46;

        public VoiceChatPanelResizeController(VoiceChatPanelResizeView view, IVoiceChatState voiceChatState)
        {
            this.view = view;
            this.voiceChatState = voiceChatState;

            this.voiceChatState.CurrentVoiceChatPanelSize.OnUpdate += OnUpdateVoiceChatPanelSize;
        }

        private void OnUpdateVoiceChatPanelSize(VoiceChatPanelSize chatPanelSize)
        {
            if (chatPanelSize == VoiceChatPanelSize.DEFAULT)
                view.VoiceChatPanelLayoutElement.preferredHeight =
                    voiceChatState.CurrentVoiceChatType.Value == VoiceChatType.PRIVATE ? COLLAPSED_PRIVATE_VOICE_CHAT_SIZE : COLLAPSED_COMMUNITY_VOICE_CHAT_SIZE;
            else
                view.VoiceChatPanelLayoutElement.preferredHeight =
                    voiceChatState.CurrentVoiceChatType.Value == VoiceChatType.PRIVATE ? EXPANDED_PRIVATE_VOICE_CHAT_SIZE : EXPANDED_COMMUNITY_VOICE_CHAT_SIZE;
        }

        public void Dispose()
        {
        }
    }
}
