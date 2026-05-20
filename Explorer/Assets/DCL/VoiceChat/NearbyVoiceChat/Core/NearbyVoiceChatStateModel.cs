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
        OPEN_MIC,
        SUPPRESSED, // when you have another more priority voice chat - Private or Community
    }

    public enum SuppressionReason
    {
        /// <summary>Initial world loading is in progress.</summary>
        LOADING,
        /// <summary>Higher-priority Community or Private call is active.</summary>
        CALL,
        /// <summary>Current scene disables Nearby voice chat via feature toggles.</summary>
        SCENE,
        /// <summary>Local player is banned from the current scene.</summary>
        SCENE_BAN,
    }

    public enum NearbyVoiceActivation
    {
        PUSH_TO_TALK,   // Hold [T]
        BUTTON,         // Widget speak button click
        FOCUS_RESUMED,  // Auto-resume after application regained focus
    }

    public class NearbyVoiceChatStateModel : IDisposable
    {
        private readonly ReactiveProperty<NearbyVoiceChatState> state;
        private readonly ReactiveProperty<SuppressionReason?> activeSuppression = new (null);
        private readonly HashSet<SuppressionReason> suppressionReasons = new ();

        private NearbyVoiceChatState preBlockedState;

        public IReadonlyReactiveProperty<NearbyVoiceChatState> State => state;
        public IReadonlyReactiveProperty<SuppressionReason?> ActiveSuppression => activeSuppression;

        /// <summary>
        ///     How the current (or most recent) SPEAKING state was entered.
        ///     Set by <see cref="StartSpeaking"/> right before the state transition.
        /// </summary>
        public NearbyVoiceActivation CurrentActivation { get; private set; }

        /// <summary>
        ///     True when the LiveKit server detects the local participant is actually producing sound (VAD).
        ///     Updated from <see cref="LiveKit.Rooms.ActiveSpeakers.IActiveSpeakers"/>.
        ///     Written on the LiveKit event thread, read on the Unity main thread — volatile ensures visibility.
        /// </summary>
        private volatile bool isLocalSpeaking;

        public bool IsLocalSpeaking
        {
            get => isLocalSpeaking;
            set => isLocalSpeaking = value;
        }

        public bool IsListeningDisabled => state.Value is NearbyVoiceChatState.SUPPRESSED or NearbyVoiceChatState.DISABLED;

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
        public void StartSpeaking(NearbyVoiceActivation activation = NearbyVoiceActivation.BUTTON)
        {
            if (state.Value == NearbyVoiceChatState.IDLE)
            {
                CurrentActivation = activation;
                SetState(NearbyVoiceChatState.OPEN_MIC);
            }
        }

        public void StopSpeaking()
        {
            if (state.Value == NearbyVoiceChatState.OPEN_MIC)
                SetState(NearbyVoiceChatState.IDLE);
        }

        // Suppression
        public void Suppress(SuppressionReason reason)
        {
            if (!suppressionReasons.Add(reason))
                return;

            activeSuppression.Value = reason;

            if (state.Value != NearbyVoiceChatState.SUPPRESSED)
            {
                if (state.Value == NearbyVoiceChatState.OPEN_MIC)
                    StopSpeaking();

                preBlockedState = state.Value;
                SetState(NearbyVoiceChatState.SUPPRESSED);
            }
        }

        public void Resume(SuppressionReason reason)
        {
            if (!suppressionReasons.Remove(reason))
                return;

            if (TryResetToRemainedSuppression(activeSuppression))
                return;

            if (state.Value == NearbyVoiceChatState.SUPPRESSED)
                SetState(preBlockedState);
        }

        private bool TryResetToRemainedSuppression(ReactiveProperty<SuppressionReason?> active)
        {
            using HashSet<SuppressionReason>.Enumerator e = suppressionReasons.GetEnumerator();

            if (e.MoveNext())
            {
                active.Value = e.Current;
                return true;
            }

            active.Value = null;
            return false;
        }

        private void SetState(NearbyVoiceChatState newState)
        {
            ReportHub.Log(ReportCategory.NEARBY_VOICE_CHAT, $"State change {state.Value} -> {newState}");
            state.Value = newState;
        }
    }
}
