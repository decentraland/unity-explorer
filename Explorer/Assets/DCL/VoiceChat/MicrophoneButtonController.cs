using System;
using System.Collections.Generic;

namespace DCL.VoiceChat
{
    public class MicrophoneButtonController : IDisposable
    {
        private readonly List<MicrophoneButton> views;
        private readonly VoiceChatMicrophoneHandler voiceChatMicrophoneHandler;

        public MicrophoneButtonController(List<MicrophoneButton> views, VoiceChatMicrophoneHandler voiceChatMicrophoneHandler)
        {
            this.views = views;
            this.voiceChatMicrophoneHandler = voiceChatMicrophoneHandler;

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
            foreach (MicrophoneButton microphoneButton in views)
                microphoneButton.SetMicrophoneStatus(true);
        }

        private void OnDisabledMicrophone()
        {
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
