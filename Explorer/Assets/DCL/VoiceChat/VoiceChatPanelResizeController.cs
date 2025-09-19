using DCL.Utilities;
using System;

namespace DCL.VoiceChat
{
    public class VoiceChatPanelResizeController : IDisposable
    {
        private const float DEFAULT_VOICE_CHAT_SIZE = 50;
        //private const float EXPANDED_COMMUNITY_VOICE_CHAT_SIZE = 240; Legacy value, kept for now
        private const int EXPANDED_COMMUNITY_VOICE_CHAT_1_LINE_SIZE = 215;
        private const int EXPANDED_COMMUNITY_VOICE_CHAT_2_LINES_SIZE = 305;
        private const int COLLAPSED_COMMUNITY_VOICE_CHAT_SIZE = 50;
        //private const float EXPANDED_PRIVATE_VOICE_CHAT_SIZE = 100; Not used yet value, kept for now
        private const int COLLAPSED_PRIVATE_VOICE_CHAT_SIZE = 50;
        private const int HIDDEN_BUTTONS_SIZE_DIFFERENCE = 40;
        private const int MAX_SPEAKERS_PER_LINE = 4;

        private readonly VoiceChatPanelResizeView view;
        private readonly IVoiceChatOrchestratorState voiceChatState;
        private readonly IDisposable panelSizeUpdateSubscription;
        private readonly IDisposable typeChangedSubscription;


        public VoiceChatPanelResizeController(VoiceChatPanelResizeView view, IVoiceChatOrchestratorState voiceChatState)
        {
            this.view = view;
            this.voiceChatState = voiceChatState;

            panelSizeUpdateSubscription = voiceChatState.CurrentVoiceChatPanelSize.Subscribe(OnUpdateVoiceChatPanelSize);
            typeChangedSubscription = voiceChatState.CurrentVoiceChatType.Subscribe(OnCurrentVoiceChatTypeChanged);

            voiceChatState.ParticipantsStateService.SpeakersUpdated += OnSpeakersUpdated;
        }

        private void OnSpeakersUpdated(int speakersAmount)
        {
            if (voiceChatState.CurrentVoiceChatType.Value != VoiceChatType.COMMUNITY) return;

            if (voiceChatState.CurrentVoiceChatPanelSize.Value == VoiceChatPanelSize.COLLAPSED) return;

            CalculateCommunitiesLayoutHeight(speakersAmount);
        }

        private void CalculateCommunitiesLayoutHeight(int speakersAmount)
        {
            int newHeight = speakersAmount <= MAX_SPEAKERS_PER_LINE ? EXPANDED_COMMUNITY_VOICE_CHAT_1_LINE_SIZE : EXPANDED_COMMUNITY_VOICE_CHAT_2_LINES_SIZE;

            view.VoiceChatPanelLayoutElement.preferredHeight = newHeight - (voiceChatState.CurrentVoiceChatPanelSize.Value == VoiceChatPanelSize.EXPANDED ? 0 : HIDDEN_BUTTONS_SIZE_DIFFERENCE);
        }

        private void OnCurrentVoiceChatTypeChanged(VoiceChatType type)
        {
            switch (type)
            {
                case VoiceChatType.PRIVATE:
                    view.VoiceChatPanelLayoutElement.preferredHeight = COLLAPSED_PRIVATE_VOICE_CHAT_SIZE;
                    break;
                case VoiceChatType.COMMUNITY:
                    CalculateCommunitiesLayoutHeight(voiceChatState.ParticipantsStateService.ActiveSpeakers.Count);
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
                        case VoiceChatPanelSize.EXPANDED:
                        case VoiceChatPanelSize.EXPANDED_WITHOUT_BUTTONS:
                            view.gameObject.SetActive(true);
                            CalculateCommunitiesLayoutHeight(voiceChatState.ParticipantsStateService.ActiveSpeakers.Count);
                            break;
                        case VoiceChatPanelSize.HIDDEN:
                            view.gameObject.SetActive(false);
                            break;
                        default:
                            view.gameObject.SetActive(true);
                            view.VoiceChatPanelLayoutElement.preferredHeight = COLLAPSED_COMMUNITY_VOICE_CHAT_SIZE;
                            break;
                    }
                    break;
            }
        }

        public void Dispose()
        {
            panelSizeUpdateSubscription.Dispose();
            typeChangedSubscription.Dispose();
        }
    }
}
