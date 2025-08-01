using DCL.Audio;
using DCL.Utilities;
using System;

namespace DCL.VoiceChat
{
    public class MicrophoneAudioToggleController : IDisposable
    {
        private readonly VoiceChatMicrophoneHandler voiceChatMicrophoneHandler;
        private readonly AudioClipConfig muteMicrophoneAudio;
        private readonly AudioClipConfig unmuteMicrophoneAudio;
        private IDisposable? microphoneStateSubscription;

        public MicrophoneAudioToggleController(
            VoiceChatMicrophoneHandler voiceChatMicrophoneHandler,
            AudioClipConfig muteMicrophoneAudio,
            AudioClipConfig unmuteMicrophoneAudio)
        {
            this.voiceChatMicrophoneHandler = voiceChatMicrophoneHandler;
            this.muteMicrophoneAudio = muteMicrophoneAudio;
            this.unmuteMicrophoneAudio = unmuteMicrophoneAudio;

            microphoneStateSubscription = voiceChatMicrophoneHandler.IsMicrophoneEnabled.Subscribe(OnMicrophoneStateChanged);
        }

        private void OnMicrophoneStateChanged(bool isEnabled)
        {
            if (isEnabled)
                UIAudioEventsBus.Instance.SendPlayAudioEvent(unmuteMicrophoneAudio);
            else
                UIAudioEventsBus.Instance.SendPlayAudioEvent(muteMicrophoneAudio);
        }

        public void Dispose()
        {
            microphoneStateSubscription?.Dispose();
            microphoneStateSubscription = null;
        }
    }
} 