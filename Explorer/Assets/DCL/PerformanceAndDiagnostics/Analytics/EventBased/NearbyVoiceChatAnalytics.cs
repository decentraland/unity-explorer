using DCL.Utilities;
using DCL.VoiceChat.Nearby;
using Newtonsoft.Json.Linq;
using System;

namespace DCL.PerformanceAndDiagnostics.Analytics.EventBased
{
    /// <summary>
    ///     Tracks nearby voice chat usage: speak activations, proximity voice on/off toggle, and per-user mutes.
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

            // IDLE → SPEAKING: new speaking session. Ignore focus-resumed — it's a continuation, not a fresh use.
            if (prev == NearbyVoiceChatState.IDLE && next == NearbyVoiceChatState.SPEAKING)
            {
                if (stateModel.CurrentActivation == NearbyVoiceActivation.FOCUS_RESUMED)
                    return;

                analytics.Track(AnalyticsEvents.VoiceChat.NEARBY_VOICE_SPEAK, new JObject
                {
                    { "activation", ActivationToString(stateModel.CurrentActivation) },
                });
                return;
            }

            // Only IDLE ↔ DISABLED transitions are user-driven (via Enable/Disable from the widget toggle).
            // Any transition involving SUPPRESSED is system-driven (call/scene/loading) and must be skipped.
            if (prev == NearbyVoiceChatState.IDLE && next == NearbyVoiceChatState.DISABLED)
                TrackToggle(enabled: false);
            else if (prev == NearbyVoiceChatState.DISABLED && next == NearbyVoiceChatState.IDLE)
                TrackToggle(enabled: true);
        }

        private void TrackToggle(bool enabled) =>
            analytics.Track(AnalyticsEvents.VoiceChat.NEARBY_VOICE_TOGGLE, new JObject
            {
                { "enabled", enabled },
            });

        private void OnMuteStateChanged(string walletId, bool isMuted) =>
            analytics.Track(AnalyticsEvents.VoiceChat.NEARBY_VOICE_USER_MUTE, new JObject
            {
                { "identity", walletId },
                { "is_muted", isMuted },
            });

        private static string ActivationToString(NearbyVoiceActivation activation) =>
            activation switch
            {
                NearbyVoiceActivation.PUSH_TO_TALK => "push_to_talk",
                NearbyVoiceActivation.BUTTON => "button",
                _ => activation.ToString().ToLowerInvariant(),
            };
    }
}
