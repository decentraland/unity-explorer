using DCL.Utilities;
using System;

namespace DCL.VoiceChat.Nearby
{
    /// <summary>
    ///     Wraps <see cref="NearbyVoiceChatStateModel.IsOpenMic"/> with a per-activation filter so the start/stop
    ///     speaking SFX can be conditionally silenced for push-to-talk sessions without changing the canonical state.
    ///     A UX experiment: spamming the cue every time the user taps [T] is noisy, so the cue is muted by default
    ///     for <see cref="NearbyVoiceActivation.PUSH_TO_TALK"/> and can be re-enabled at runtime via
    ///     <see cref="VoiceChatConfiguration.nearbyPlaySfxOnPushToTalk"/> (exposed in the Nearby debug widget).
    ///     The filter is sampled on each transition, so it is symmetric — if entry was muted, exit is muted too —
    ///     and toggling the flag mid-session only affects the next OPEN_MIC entry/exit.
    /// </summary>
    public class NearbySpeakingAudibilityGate : IDisposable
    {
        private readonly NearbyVoiceChatStateModel stateModel;
        private readonly VoiceChatConfiguration configuration;
        private readonly ReactiveProperty<bool> effectiveOpenMic;
        private readonly IDisposable subscription;

        public IReadonlyReactiveProperty<bool> EffectiveOpenMic => effectiveOpenMic;

        public NearbySpeakingAudibilityGate(NearbyVoiceChatStateModel stateModel, VoiceChatConfiguration configuration)
        {
            this.stateModel = stateModel;
            this.configuration = configuration;

            effectiveOpenMic = new ReactiveProperty<bool>(ComputeEffective(stateModel.IsOpenMic.Value));
            subscription = stateModel.IsOpenMic.Subscribe(OnOpenMicChanged);
        }

        public void Dispose()
        {
            subscription.Dispose();
            effectiveOpenMic.ClearSubscriptionsList();
        }

        private void OnOpenMicChanged(bool isOpenMic)
        {
            effectiveOpenMic.Value = ComputeEffective(isOpenMic);
        }

        private bool ComputeEffective(bool isOpenMic)
        {
            bool isPushToTalk = stateModel.CurrentActivation == NearbyVoiceActivation.PUSH_TO_TALK;
            return isOpenMic && (!isPushToTalk || configuration.nearbyPlaySfxOnPushToTalk);
        }
    }
}
