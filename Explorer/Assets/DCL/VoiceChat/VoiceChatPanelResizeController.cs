using System;
using UnityEngine;

namespace DCL.VoiceChat
{
    public class VoiceChatPanelResizeController : IDisposable
    {
        private readonly VoiceChatPanelResizeView view;
        private readonly IVoiceChatOrchestratorState voiceChatState;

        private const float DEFAULT_VOICE_CHAT_SIZE = 46;
        private const float EXPANDED_COMMUNITY_VOICE_CHAT_SIZE = 300;
        private const float COLLAPSED_COMMUNITY_VOICE_CHAT_SIZE = 46;
        private const float EXPANDED_PRIVATE_VOICE_CHAT_SIZE = 100;
        private const float COLLAPSED_PRIVATE_VOICE_CHAT_SIZE = 46;

        public VoiceChatPanelResizeController(VoiceChatPanelResizeView view, IVoiceChatOrchestratorState voiceChatState)
        {
            this.view = view;
            this.voiceChatState = voiceChatState;

            this.voiceChatState.CurrentVoiceChatPanelSize.OnUpdate += OnUpdateVoiceChatPanelSize;
        }

        private void OnUpdateVoiceChatPanelSize(VoiceChatPanelSize chatPanelSize)
        {
            switch (voiceChatState.CurrentVoiceChatType.Value)
            {
                case VoiceChatType.NONE:
                    view.VoiceChatPanelLayoutElement.preferredHeight = DEFAULT_VOICE_CHAT_SIZE;
                    break;
                case VoiceChatType.PRIVATE:
                    view.VoiceChatPanelLayoutElement.preferredHeight =
                        chatPanelSize == VoiceChatPanelSize.DEFAULT ? COLLAPSED_PRIVATE_VOICE_CHAT_SIZE : EXPANDED_PRIVATE_VOICE_CHAT_SIZE;
                    break;
                case VoiceChatType.COMMUNITY:
                    view.VoiceChatPanelLayoutElement.preferredHeight =
                        chatPanelSize == VoiceChatPanelSize.DEFAULT ? COLLAPSED_COMMUNITY_VOICE_CHAT_SIZE : EXPANDED_COMMUNITY_VOICE_CHAT_SIZE;
                    break;
            }
        }

        public void Dispose()
        {
        }
    }
}
