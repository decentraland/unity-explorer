using DCL.Diagnostics;
using DCL.Utilities;
using System;
using System.Collections.Generic;

namespace DCL.VoiceChat.Nearby
{
    public enum NearbyVoiceChatState
    {
        Disabled,
        Idle, // default state where user is connected to nearby chat and can hear its participants
        OpenMic,
        Suppressed, // when you have another more priority voice chat - Private or Community
    }

    public enum SuppressionReason
    {
        /// <summary>Initial world loading is in progress.</summary>
        Loading,
        /// <summary>Higher-priority Community or Private call is active.</summary>
        Call,
        /// <summary>Current scene disables Nearby voice chat via feature toggles.</summary>
        Scene,
        /// <summary>Local player is banned from the current scene.</summary>
        SceneBan,
    }

    public enum NearbyVoiceActivation
    {
        PushToTalk,   // Hold [T]
        Button,         // Widget speak button click
        FocusResumed,  // Auto-resume after application regained focus
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

        public bool IsListeningDisabled => state.Value is NearbyVoiceChatState.Suppressed or NearbyVoiceChatState.Disabled;

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
            if (state.Value == NearbyVoiceChatState.Disabled)
                SetState(NearbyVoiceChatState.Idle);
        }

        public void Disable()
        {
            SetState(NearbyVoiceChatState.Disabled);
        }

        // Speaking
        public void StartSpeaking(NearbyVoiceActivation activation = NearbyVoiceActivation.Button)
        {
            if (state.Value == NearbyVoiceChatState.Idle)
            {
                CurrentActivation = activation;
                SetState(NearbyVoiceChatState.OpenMic);
            }
        }

        public void StopSpeaking()
        {
            if (state.Value == NearbyVoiceChatState.OpenMic)
                SetState(NearbyVoiceChatState.Idle);
        }

        // Suppression
        public void Suppress(SuppressionReason reason)
        {
            if (!suppressionReasons.Add(reason))
                return;

            activeSuppression.Value = reason;

            if (state.Value != NearbyVoiceChatState.Suppressed)
            {
                if (state.Value == NearbyVoiceChatState.OpenMic)
                    StopSpeaking();

                preBlockedState = state.Value;
                SetState(NearbyVoiceChatState.Suppressed);
            }
        }

        public void Resume(SuppressionReason reason)
        {
            if (!suppressionReasons.Remove(reason))
                return;

            if (TryResetToRemainedSuppression(activeSuppression))
                return;

            if (state.Value == NearbyVoiceChatState.Suppressed)
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
