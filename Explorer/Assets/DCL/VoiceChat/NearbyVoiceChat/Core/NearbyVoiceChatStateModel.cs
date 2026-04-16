using DCL.Diagnostics;
using DCL.Utilities;
using System;
using System.Collections.Generic;

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
        public const string SUPPRESSION_CALL = "call";
        public const string SUPPRESSION_SCENE = "scene";

        private readonly ReactiveProperty<NearbyVoiceChatState> state;
        private readonly ReactiveProperty<string?> activeSuppression = new (null);
        private readonly HashSet<string> suppressionReasons = new ();

        private NearbyVoiceChatState preBlockedState;

        public IReadonlyReactiveProperty<NearbyVoiceChatState> State => state;

        public IReadonlyReactiveProperty<string?> ActiveSuppression => activeSuppression;

        public NearbyVoiceChatStateModel(NearbyVoiceChatState initialState)
        {
            state = new ReactiveProperty<NearbyVoiceChatState>(initialState);
            preBlockedState = initialState;
        }

        public void Dispose()
        {
            state.ClearSubscriptionsList();
            activeSuppression.ClearSubscriptionsList();
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
        public void Suppress() => Suppress(SUPPRESSION_CALL);

        public void Suppress(string reason)
        {
            if (!suppressionReasons.Add(reason))
                return;

            activeSuppression.Value = reason;

            if (state.Value == NearbyVoiceChatState.SUPPRESSED)
                return;

            preBlockedState = state.Value;
            SetState(NearbyVoiceChatState.SUPPRESSED);
        }

        public void Resume() => Resume(SUPPRESSION_CALL);

        public void Resume(string reason)
        {
            if (!suppressionReasons.Remove(reason))
                return;

            if (suppressionReasons.Count > 0)
            {
                foreach (string remaining in suppressionReasons)
                {
                    activeSuppression.Value = remaining;
                    break;
                }

                return;
            }

            activeSuppression.Value = null;

            if (state.Value != NearbyVoiceChatState.SUPPRESSED)
                return;

            SetState(preBlockedState);
        }

        private void SetState(NearbyVoiceChatState newState)
        {
            ReportHub.Log(ReportCategory.NEARBY_VOICE_CHAT, $"State change {state.Value} -> {newState}");
            state.Value = newState;
        }
    }
}
