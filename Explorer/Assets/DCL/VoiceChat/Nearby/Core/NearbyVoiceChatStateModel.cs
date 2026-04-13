using DCL.Diagnostics;
using DCL.Utilities;
using System;

namespace DCL.VoiceChat.Nearby
{
    public enum NearbyVoiceChatState
    {
        DISABLED,
        HEARING,
        SPEAKING,
        SUPPRESSED, // when you have another more priority voice chat - Nearby or Community
    }

    public class NearbyVoiceChatStateModel : IDisposable
    {
        private readonly ReactiveProperty<NearbyVoiceChatState> state;

        private NearbyVoiceChatState preBlockedState;

        public IReadonlyReactiveProperty<NearbyVoiceChatState> State => state;

        public NearbyVoiceChatStateModel(NearbyVoiceChatState initialState)
        {
            state = new ReactiveProperty<NearbyVoiceChatState>(initialState);
            preBlockedState = initialState;
        }

        public void Dispose()
        {
            state.ClearSubscriptionsList();
        }

        public void Enable()
        {
            if (state.Value == NearbyVoiceChatState.DISABLED)
                SetState(NearbyVoiceChatState.HEARING);
        }

        public void Disable()
        {
            SetState(NearbyVoiceChatState.DISABLED);
        }

        // Speaking
        public void StartSpeaking()
        {
            if (state.Value == NearbyVoiceChatState.HEARING)
                SetState(NearbyVoiceChatState.SPEAKING);
        }

        public void StopSpeaking()
        {
            if (state.Value == NearbyVoiceChatState.SPEAKING)
                SetState(NearbyVoiceChatState.HEARING);
        }

        // Suppression
        public void Suppress()
        {
            if (state.Value == NearbyVoiceChatState.SUPPRESSED)
                return;

            preBlockedState = state.Value;
            SetState(NearbyVoiceChatState.SUPPRESSED);
        }

        public void Resume()
        {
            if (state.Value == NearbyVoiceChatState.SUPPRESSED)
                SetState(preBlockedState);
        }

        private void SetState(NearbyVoiceChatState newState)
        {
            ReportHub.Log(ReportCategory.NEARBY_VOICE_CHAT, $"State change {state.Value} -> {newState}");
            state.Value = newState;
        }
    }
}
