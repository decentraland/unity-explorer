using DCL.Audio;
using System;
using System.Collections.Generic;

namespace DCL.VoiceChat
{
    public class MicrophoneButtonController : IDisposable
    {
        private readonly List<MicrophoneButton> views;
        private readonly VoiceChatMicrophoneHandler voiceChatMicrophoneHandler;
        private readonly AudioClipConfig muteMicrophoneAudio;
        private readonly AudioClipConfig unmuteMicrophoneAudio;

        public MicrophoneButtonController(
            List<MicrophoneButton> views,
            VoiceChatMicrophoneHandler voiceChatMicrophoneHandler,
            AudioClipConfig muteMicrophoneAudio,
            AudioClipConfig unmuteMicrophoneAudio)
        {
            this.views = views;
            this.voiceChatMicrophoneHandler = voiceChatMicrophoneHandler;
            this.muteMicrophoneAudio = muteMicrophoneAudio;
            this.unmuteMicrophoneAudio = unmuteMicrophoneAudio;

            foreach (MicrophoneButton microphoneButton in views)
                microphoneButton.MicButton.onClick.AddListener(ToggleVoiceChat);

            this.voiceChatMicrophoneHandler.EnabledMicrophone += OnEnabledMicrophone;
            this.voiceChatMicrophoneHandler.DisabledMicrophone += OnDisabledMicrophone;
        }

        private void ToggleVoiceChat()
        {
            voiceChatMicrophoneHandler.ToggleMicrophone();
        }

        private void OnEnabledMicrophone()
        {
            UIAudioEventsBus.Instance.SendPlayAudioEvent(unmuteMicrophoneAudio);
            foreach (MicrophoneButton microphoneButton in views)
                microphoneButton.SetMicrophoneStatus(true);
        }

        private void OnDisabledMicrophone()
        {
            UIAudioEventsBus.Instance.SendPlayAudioEvent(muteMicrophoneAudio);
            foreach (MicrophoneButton microphoneButton in views)
                microphoneButton.SetMicrophoneStatus(false);
        }

        public void Dispose()
        {
            voiceChatMicrophoneHandler.EnabledMicrophone -= OnEnabledMicrophone;
            voiceChatMicrophoneHandler.DisabledMicrophone -= OnDisabledMicrophone;
        }
    }
}
