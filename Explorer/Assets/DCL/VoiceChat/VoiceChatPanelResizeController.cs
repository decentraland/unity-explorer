using DCL.Utilities;
using System;

namespace DCL.VoiceChat
{
    public class VoiceChatPanelResizeController : IDisposable
    {
        private readonly VoiceChatPanelResizeView view;
        private readonly IVoiceChatOrchestratorState voiceChatState;
        private readonly IDisposable voiceChatPanelSizeUpdateSubscription;
        private readonly IDisposable voiceChatTypeChangedSubscription;

        private const float DEFAULT_VOICE_CHAT_SIZE = 50;
        private const float EXPANDED_COMMUNITY_VOICE_CHAT_SIZE = 240;
        private const float COLLAPSED_COMMUNITY_VOICE_CHAT_SIZE = 50;
        private const float EXPANDED_PRIVATE_VOICE_CHAT_SIZE = 100;
        private const float COLLAPSED_PRIVATE_VOICE_CHAT_SIZE = 50;
        private const float HIDDEN_BUTTONS_SIZE_DIFFERENCE = 40;

        public VoiceChatPanelResizeController(VoiceChatPanelResizeView view, IVoiceChatOrchestratorState voiceChatState)
        {
            this.view = view;
            this.voiceChatState = voiceChatState;

            voiceChatPanelSizeUpdateSubscription = voiceChatState.CurrentVoiceChatPanelSize.Subscribe(OnUpdateVoiceChatPanelSize);
            voiceChatTypeChangedSubscription = voiceChatState.CurrentVoiceChatType.Subscribe(OnCurrentVoiceChatTypeChanged);
        }

        private void OnCurrentVoiceChatTypeChanged(VoiceChatType type)
        {
            switch (type)
            {
                case VoiceChatType.PRIVATE:
                    view.VoiceChatPanelLayoutElement.preferredHeight = COLLAPSED_PRIVATE_VOICE_CHAT_SIZE;
                    break;
                case VoiceChatType.COMMUNITY:
                    view.VoiceChatPanelLayoutElement.preferredHeight = EXPANDED_COMMUNITY_VOICE_CHAT_SIZE;
                    break;
                case VoiceChatType.NONE:
                default:
                    view.VoiceChatPanelLayoutElement.preferredHeight = DEFAULT_VOICE_CHAT_SIZE;
                    break;
            }
        }

        private void OnUpdateVoiceChatPanelSize(VoiceChatPanelSize chatPanelSize)
        {
            switch (voiceChatState.CurrentVoiceChatType.Value)
            {
                case VoiceChatType.NONE:
                    view.VoiceChatPanelLayoutElement.preferredHeight = DEFAULT_VOICE_CHAT_SIZE;
                    break;
                case VoiceChatType.PRIVATE:
                    if (chatPanelSize == VoiceChatPanelSize.HIDDEN) { view.gameObject.SetActive(false); }
                    else
                    {
                        view.gameObject.SetActive(true);
                        view.VoiceChatPanelLayoutElement.preferredHeight = COLLAPSED_PRIVATE_VOICE_CHAT_SIZE;
                    }
                    break;
                case VoiceChatType.COMMUNITY:
                    switch (chatPanelSize)
                    {
                        case VoiceChatPanelSize.EXPANDED_WITHOUT_BUTTONS:
                            //First steps to handle dynamic sizing
                            view.gameObject.SetActive(true);
                            view.VoiceChatPanelLayoutElement.preferredHeight = EXPANDED_COMMUNITY_VOICE_CHAT_SIZE - HIDDEN_BUTTONS_SIZE_DIFFERENCE; break;
                        case VoiceChatPanelSize.HIDDEN:
                            view.gameObject.SetActive(false);
                            break;
                        default:
                            view.gameObject.SetActive(true);
                            view.VoiceChatPanelLayoutElement.preferredHeight =
                                chatPanelSize == VoiceChatPanelSize.COLLAPSED ? COLLAPSED_COMMUNITY_VOICE_CHAT_SIZE : EXPANDED_COMMUNITY_VOICE_CHAT_SIZE; break;
                    }
                    break;
            }
        }

        public void Dispose()
        {
            voiceChatPanelSizeUpdateSubscription.Dispose();
            voiceChatTypeChangedSubscription.Dispose();
        }
    }
}
