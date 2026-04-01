using DCL.Diagnostics;
using DCL.Utilities;
using DCL.VoiceChat.Proximity.UI;
using System;

namespace DCL.VoiceChat.Proximity
{
    public class ProximityVoiceChatStateModel : IDisposable
    {
        private readonly ReactiveProperty<ProximityVoiceChatState> state;

        private ProximityVoiceChatState preBlockedState;

        public IReadonlyReactiveProperty<ProximityVoiceChatState> State => state;

        public ProximityVoiceChatStateModel(ProximityVoiceChatState initialState)
        {
            state = new ReactiveProperty<ProximityVoiceChatState>(initialState);
            preBlockedState = initialState;
        }

        public void Dispose()
        {
            state.ClearSubscriptionsList();
        }

        public void Enable()
        {
            if (state.Value == ProximityVoiceChatState.DISABLED)
                SetState(ProximityVoiceChatState.HEARING);
        }

        public void Disable()
        {
            SetState(ProximityVoiceChatState.DISABLED);
        }

        // Speaking
        public void StartSpeaking()
        {
            if (state.Value == ProximityVoiceChatState.HEARING)
                SetState(ProximityVoiceChatState.SPEAKING);
        }

        public void StopSpeaking()
        {
            if (state.Value == ProximityVoiceChatState.SPEAKING)
                SetState(ProximityVoiceChatState.HEARING);
        }

        // Suppression
        public void Suppress()
        {
            if (state.Value == ProximityVoiceChatState.SUPPRESSED)
                return;

            preBlockedState = state.Value;
            SetState(ProximityVoiceChatState.SUPPRESSED);
        }

        public void Resume()
        {
            if (state.Value == ProximityVoiceChatState.SUPPRESSED)
                SetState(preBlockedState);
        }

        private void SetState(ProximityVoiceChatState newState)
        {
            ReportHub.Log(ReportCategory.PROXIMITY_VOICE_CHAT, $"State change {state.Value} -> {newState}");
            state.Value = newState;
        }
    }
}
