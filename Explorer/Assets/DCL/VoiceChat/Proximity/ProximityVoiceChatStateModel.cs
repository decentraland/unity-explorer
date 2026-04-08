using DCL.Diagnostics;
using DCL.Utilities;
using System;
using System.Collections.Generic;

namespace DCL.VoiceChat
{
    public class ProximityVoiceChatStateModel : IDisposable
    {
        public const string SUPPRESSION_CALL = "call";
        public const string SUPPRESSION_SCENE = "scene";

        private const string TAG = nameof(ProximityVoiceChatStateModel);

        private readonly ReactiveProperty<ProximityVoiceChatState> state;
        private readonly HashSet<string> suppressionReasons = new ();

        private ProximityVoiceChatState preBlockedState;

        public ProximityVoiceChatStateModel(ProximityVoiceChatState initialState = ProximityVoiceChatState.Hearing)
        {
            state = new ReactiveProperty<ProximityVoiceChatState>(initialState);
            preBlockedState = initialState;
        }

        public IReadonlyReactiveProperty<ProximityVoiceChatState> State => state;

        public void Enable()
        {
            if (state.Value != ProximityVoiceChatState.Disconnected)
                return;

            SetState(ProximityVoiceChatState.Hearing);
        }

        public void Disable()
        {
            if (state.Value is not (ProximityVoiceChatState.Hearing or ProximityVoiceChatState.Speaking))
                return;

            SetState(ProximityVoiceChatState.Disconnected);
        }

        public void StartSpeaking()
        {
            if (state.Value != ProximityVoiceChatState.Hearing)
                return;

            SetState(ProximityVoiceChatState.Speaking);
        }

        public void StopSpeaking()
        {
            if (state.Value != ProximityVoiceChatState.Speaking)
                return;

            SetState(ProximityVoiceChatState.Hearing);
        }

        public void Suppress() => Suppress(SUPPRESSION_CALL);

        public void Suppress(string reason)
        {
            if (!suppressionReasons.Add(reason))
                return;

            if (state.Value == ProximityVoiceChatState.Blocked)
                return;

            preBlockedState = state.Value;
            SetState(ProximityVoiceChatState.Blocked);
        }

        public void Resume() => Resume(SUPPRESSION_CALL);

        public void Resume(string reason)
        {
            if (!suppressionReasons.Remove(reason))
                return;

            if (suppressionReasons.Count > 0)
                return;

            if (state.Value != ProximityVoiceChatState.Blocked)
                return;

            SetState(preBlockedState);
        }

        public void Dispose()
        {
            state.ClearSubscriptionsList();
        }

        private void SetState(ProximityVoiceChatState newState)
        {
            ProximityVoiceChatState previous = state.Value;
            state.Value = newState;
            ReportHub.Log(ReportCategory.VOICE_CHAT, $"{TAG} {previous} -> {newState}");
        }
    }
}
