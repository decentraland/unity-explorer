using DCL.Audio;
using DCL.Utilities;
using System;

namespace DCL.VoiceChat
{
    public class MicrophoneAudioToggleHandler : IDisposable
    {
        private readonly AudioClipConfig muteMicrophoneAudio;
        private readonly AudioClipConfig unmuteMicrophoneAudio;
        private readonly IDisposable? microphoneStateSubscription;

        public MicrophoneAudioToggleHandler(
            VoiceChatMicrophoneHandler voiceChatMicrophoneHandler,
            AudioClipConfig muteMicrophoneAudio,
            AudioClipConfig unmuteMicrophoneAudio)
        {
            this.muteMicrophoneAudio = muteMicrophoneAudio;
            this.unmuteMicrophoneAudio = unmuteMicrophoneAudio;

            microphoneStateSubscription = voiceChatMicrophoneHandler.IsMicrophoneEnabled.Subscribe(OnMicrophoneStateChanged);
        }

        public void Dispose()
        {
            microphoneStateSubscription?.Dispose();
        }

        private void OnMicrophoneStateChanged(bool isEnabled)
        {
            UIAudioEventsBus.Instance.SendPlayAudioEvent(isEnabled ? unmuteMicrophoneAudio : muteMicrophoneAudio);
        }
    }
}
