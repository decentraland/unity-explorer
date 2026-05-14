using DCL.Audio;
using DCL.Utilities;
using System;

namespace DCL.VoiceChat.Nearby
{
    /// <summary>
    ///     Plays start/stop speaking SFX on user-driven <see cref="NearbyVoiceChatState.IDLE"/> ↔
    ///     <see cref="NearbyVoiceChatState.OPEN_MIC"/> transitions only.
    ///     System-driven transitions stay silent so forced mutes never sound like a user toggle:
    ///     a Suppress() force-stop is detected by a non-null <see cref="NearbyVoiceChatStateModel.ActiveSuppression"/>
    ///     at the OPEN_MIC → IDLE tick (Suppress sets the reason before stopping), and a system Resume()
    ///     is filtered out because it re-enters OPEN_MIC from SUPPRESSED, not from IDLE.
    ///     Push-to-talk activations play at a reduced volume (see <see cref="VoiceChatConfiguration.NearbyPushToTalkVolumeScale"/>)
    ///     so rapid taps stay subtle; other activations play at full.
    /// </summary>
    public class NearbyMicrophoneAudioToggleHandler : IDisposable
    {
        private const float DEFAULT_VOLUME_SCALE = 1f;

        private readonly NearbyVoiceChatStateModel stateModel;
        private readonly VoiceChatConfiguration configuration;
        private readonly AudioClipConfig offAudio;
        private readonly AudioClipConfig onAudio;
        private readonly IDisposable stateSubscription;

        private NearbyVoiceChatState previousState;

        public NearbyMicrophoneAudioToggleHandler(NearbyVoiceChatStateModel stateModel, VoiceChatConfiguration configuration, AudioClipConfig offAudio, AudioClipConfig onAudio)
        {
            this.stateModel = stateModel;
            this.configuration = configuration;
            this.offAudio = offAudio;
            this.onAudio = onAudio;

            previousState = stateModel.State.Value;
            stateSubscription = stateModel.State.Subscribe(OnStateChanged);
        }

        public void Dispose()
        {
            stateSubscription.Dispose();
        }

        private void OnStateChanged(NearbyVoiceChatState newState)
        {
            NearbyVoiceChatState prev = previousState;
            previousState = newState;

            bool isUserStart = prev == NearbyVoiceChatState.IDLE && newState == NearbyVoiceChatState.OPEN_MIC;
            bool isUserStop = prev == NearbyVoiceChatState.OPEN_MIC && newState == NearbyVoiceChatState.IDLE
                                                                   && stateModel.ActiveSuppression.Value == null;

            if (!isUserStart && !isUserStop) return;

            float volumeScale = stateModel.CurrentActivation == NearbyVoiceActivation.PUSH_TO_TALK
                ? configuration.NearbyPushToTalkVolumeScale
                : DEFAULT_VOLUME_SCALE;

            UIAudioEventsBus.Instance.SendPlayAudioEvent(isUserStart ? onAudio : offAudio, volumeScale);
        }
    }
}
