using DCL.Utilities;
using DCL.VoiceChat.Nearby;
using Newtonsoft.Json.Linq;
using System;

namespace DCL.PerformanceAndDiagnostics.Analytics.EventBased
{
    /// <summary>
    ///     Tracks nearby voice chat usage: button-toggle and push-to-talk speak events,
    ///     proximity voice on/off toggle, and per-user mutes.
    ///     Listens to <see cref="NearbyVoiceChatStateModel.State"/> transitions and <see cref="NearbyMuteService.MuteStateChanged"/>.
    /// </summary>
    public class NearbyVoiceChatAnalytics : IDisposable
    {
        private readonly IAnalyticsController analytics;
        private readonly NearbyVoiceChatStateModel stateModel;
        private readonly NearbyMuteService muteService;
        private readonly ReactivePropertyExtensions.DisposableSubscription<NearbyVoiceChatState> stateSubscription;

        private NearbyVoiceChatState prevState;

        public NearbyVoiceChatAnalytics(IAnalyticsController analytics, NearbyVoiceChatStateModel stateModel, NearbyMuteService muteService)
        {
            this.analytics = analytics;
            this.stateModel = stateModel;
            this.muteService = muteService;

            prevState = stateModel.State.Value;
            stateSubscription = stateModel.State.Subscribe(OnStateChanged);
            muteService.MuteStateChanged += OnMuteStateChanged;
        }

        public void Dispose()
        {
            stateSubscription.Dispose();
            muteService.MuteStateChanged -= OnMuteStateChanged;
        }

        private void OnStateChanged(NearbyVoiceChatState next)
        {
            NearbyVoiceChatState prev = prevState;
            prevState = next;

            // IDLE → SPEAKING: dispatch by activation. FOCUS_RESUMED is a continuation, not a fresh use.
            if (prev == NearbyVoiceChatState.IDLE && next == NearbyVoiceChatState.SPEAKING)
            {
                switch (stateModel.CurrentActivation)
                {
                    case NearbyVoiceActivation.BUTTON:
                        TrackSpeakButton(enabled: true);
                        break;
                    case NearbyVoiceActivation.PUSH_TO_TALK:
                        analytics.Track(AnalyticsEvents.VoiceChat.NEARBY_VOICE_SPEAK_PTT);
                        break;
                }
                return;
            }

            // SPEAKING → IDLE: button-toggle off. Skip suppression-driven stops (call/scene/loading) — system, not user.
            if (prev == NearbyVoiceChatState.SPEAKING && next == NearbyVoiceChatState.IDLE
                && stateModel.CurrentActivation == NearbyVoiceActivation.BUTTON
                && stateModel.ActiveSuppression.Value == null)
            {
                TrackSpeakButton(enabled: false);
                return;
            }

            // Hear-Others toggle — user-driven via the widget HearOthersToggle.
            // - Disable() is unconditional, so toggle-off can fire from IDLE or SPEAKING.
            // - Enable() is gated to DISABLED, so toggle-on is only DISABLED → IDLE.
            // Transitions involving SUPPRESSED are system-driven (call/scene/loading) and are skipped.
            if (next == NearbyVoiceChatState.DISABLED && prev is NearbyVoiceChatState.IDLE or NearbyVoiceChatState.SPEAKING)
                TrackToggle(enabled: false);
            else if (prev == NearbyVoiceChatState.DISABLED && next == NearbyVoiceChatState.IDLE)
                TrackToggle(enabled: true);
        }

        private void TrackSpeakButton(bool enabled) =>
            analytics.Track(AnalyticsEvents.VoiceChat.NEARBY_VOICE_SPEAK_BUTTON, new JObject
            {
                { "enabled", enabled },
            });

        private void TrackToggle(bool enabled) =>
            analytics.Track(AnalyticsEvents.VoiceChat.NEARBY_VOICE_TOGGLE, new JObject
            {
                { "enabled", enabled },
            });

        private void OnMuteStateChanged(string walletId, bool isMuted) =>
            analytics.Track(AnalyticsEvents.VoiceChat.NEARBY_VOICE_USER_MUTE, new JObject
            {
                { "wallet_id", walletId },
                { "is_muted", isMuted },
            });
    }
}
