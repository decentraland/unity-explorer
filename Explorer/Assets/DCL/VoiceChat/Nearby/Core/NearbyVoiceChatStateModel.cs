using DCL.Diagnostics;
using DCL.Utilities;
using System;

namespace DCL.VoiceChat.Nearby
{
    public enum NearbyVoiceChatState
    {
        DISABLED,
        IDLE, // default state where user is connected to nearby chat and can hear its participants
        SPEAKING,
        SUPPRESSED, // when you have another more priority voice chat - Private or Community
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
                SetState(NearbyVoiceChatState.IDLE);
        }

        public void Disable()
        {
            SetState(NearbyVoiceChatState.DISABLED);
        }

        // Speaking
        public void StartSpeaking()
        {
            if (state.Value == NearbyVoiceChatState.IDLE)
                SetState(NearbyVoiceChatState.SPEAKING);
        }

        public void StopSpeaking()
        {
            if (state.Value == NearbyVoiceChatState.SPEAKING)
                SetState(NearbyVoiceChatState.IDLE);
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
