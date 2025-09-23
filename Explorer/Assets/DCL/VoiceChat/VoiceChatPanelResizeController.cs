using DCL.Utilities;
using System;

namespace DCL.VoiceChat
{
    public class VoiceChatPanelResizeController : IDisposable
    {
        private const float DEFAULT_VOICE_CHAT_SIZE = 50;
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
        private readonly IDisposable panelStateChangedSubscription;


        public VoiceChatPanelResizeController(VoiceChatPanelResizeView view, IVoiceChatOrchestratorState voiceChatState)
        {
            this.view = view;
            this.voiceChatState = voiceChatState;

            panelSizeUpdateSubscription = voiceChatState.CurrentVoiceChatPanelSize.Subscribe(OnUpdateVoiceChatPanelSize);
            typeChangedSubscription = voiceChatState.CurrentVoiceChatType.Subscribe(OnCurrentVoiceChatTypeChanged);
            panelStateChangedSubscription = voiceChatState.CurrentVoiceChatPanelState.Subscribe(OnUpdateVoiceChatPanelState);
            voiceChatState.ParticipantsStateService.SpeakersUpdated += OnSpeakersUpdated;
        }

        private void OnUpdateVoiceChatPanelState(VoiceChatPanelState state)
        {
            if (state == VoiceChatPanelState.HIDDEN)
            {
                view.gameObject.SetActive(false);
                return;
            }

            if (voiceChatState.CurrentVoiceChatType.Value != VoiceChatType.COMMUNITY) return;

            if (voiceChatState.CurrentVoiceChatPanelSize.Value == VoiceChatPanelSize.COLLAPSED) return;

            CalculateExpandedCommunitiesLayoutHeight(voiceChatState.ParticipantsStateService.ActiveSpeakers.Count);
        }

        private void OnSpeakersUpdated(int speakersAmount)
        {
            if (voiceChatState.CurrentVoiceChatType.Value != VoiceChatType.COMMUNITY) return;

            if (voiceChatState.CurrentVoiceChatPanelSize.Value == VoiceChatPanelSize.COLLAPSED) return;

            CalculateExpandedCommunitiesLayoutHeight(speakersAmount);
        }

        private void CalculateExpandedCommunitiesLayoutHeight(int speakersAmount)
        {
            int newHeight = speakersAmount <= MAX_SPEAKERS_PER_LINE ? EXPANDED_COMMUNITY_VOICE_CHAT_1_LINE_SIZE : EXPANDED_COMMUNITY_VOICE_CHAT_2_LINES_SIZE;

            view.Resize(newHeight - (voiceChatState.CurrentVoiceChatPanelState.Value == VoiceChatPanelState.UNFOCUSED? HIDDEN_BUTTONS_SIZE_DIFFERENCE : 0));

            if (speakersAmount > MAX_SPEAKERS_PER_LINE * 2)
            {
                //Enable Scrollbar else disable it.
            }
        }

        private void OnCurrentVoiceChatTypeChanged(VoiceChatType type)
        {
            switch (type)
            {
                case VoiceChatType.PRIVATE:
                    view.Resize(COLLAPSED_PRIVATE_VOICE_CHAT_SIZE);
                    break;
                case VoiceChatType.COMMUNITY:
                    CalculateExpandedCommunitiesLayoutHeight(voiceChatState.ParticipantsStateService.ActiveSpeakers.Count);
                    break;
                case VoiceChatType.NONE:
                default:
                    view.Resize(DEFAULT_VOICE_CHAT_SIZE);
                    break;
            }
        }

        private void OnUpdateVoiceChatPanelSize(VoiceChatPanelSize chatPanelSize)
        {
            switch (voiceChatState.CurrentVoiceChatType.Value)
            {
                case VoiceChatType.NONE:
                    view.Resize(DEFAULT_VOICE_CHAT_SIZE);
                    break;
                case VoiceChatType.PRIVATE:
                        view.gameObject.SetActive(true);
                        view.Resize(COLLAPSED_PRIVATE_VOICE_CHAT_SIZE);
                    break;
                case VoiceChatType.COMMUNITY:
                    switch (chatPanelSize)
                    {
                        case VoiceChatPanelSize.EXPANDED:
                            view.gameObject.SetActive(true);
                            CalculateExpandedCommunitiesLayoutHeight(voiceChatState.ParticipantsStateService.ActiveSpeakers.Count);
                            break;
                        default:
                            view.gameObject.SetActive(true);
                            view.Resize(COLLAPSED_COMMUNITY_VOICE_CHAT_SIZE);
                            break;
                    }
                    break;
            }
        }

        public void Dispose()
        {
            panelSizeUpdateSubscription.Dispose();
            typeChangedSubscription.Dispose();
            panelStateChangedSubscription.Dispose();
        }
    }
}
