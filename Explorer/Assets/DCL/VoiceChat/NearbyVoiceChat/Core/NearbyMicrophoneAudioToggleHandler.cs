using DCL.Audio;
using DCL.Utilities;
using System;

namespace DCL.VoiceChat.Nearby
{
    /// <summary>
    ///     Nearby-flavour of <see cref="MicrophoneAudioToggleHandler"/>: plays start/stop speaking SFX on every
    ///     <see cref="NearbyVoiceChatStateModel.IsOpenMic"/> transition, attenuated for push-to-talk so the cue
    ///     reads as a subtle confirmation when the user hammers [T] but stays prominent for menu/button activations.
    ///     Volume scale is sampled at the moment of the start transition and reused for the matching stop,
    ///     so a session is symmetric in loudness.
    /// </summary>
    public class NearbyMicrophoneAudioToggleHandler : IDisposable
    {
        private const float PUSH_TO_TALK_VOLUME_SCALE = 0.2f;
        private const float DEFAULT_VOLUME_SCALE = 1f;

        private readonly NearbyVoiceChatStateModel stateModel;
        private readonly AudioClipConfig offAudio;
        private readonly AudioClipConfig onAudio;
        private readonly IDisposable stateSubscription;

        public NearbyMicrophoneAudioToggleHandler(NearbyVoiceChatStateModel stateModel, AudioClipConfig offAudio, AudioClipConfig onAudio)
        {
            this.stateModel = stateModel;
            this.offAudio = offAudio;
            this.onAudio = onAudio;

            stateSubscription = stateModel.IsOpenMic.Subscribe(OnOpenMicChanged);
        }

        public void Dispose()
        {
            stateSubscription.Dispose();
        }

        private void OnOpenMicChanged(bool isOpenMic)
        {
            float volumeScale = stateModel.CurrentActivation == NearbyVoiceActivation.PUSH_TO_TALK
                ? PUSH_TO_TALK_VOLUME_SCALE
                : DEFAULT_VOLUME_SCALE;

            UIAudioEventsBus.Instance.SendPlayAudioEvent(isOpenMic ? onAudio : offAudio, volumeScale);
        }
    }
}
