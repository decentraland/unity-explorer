using DCL.Utilities;
using System;
using System.Collections.Generic;

namespace DCL.VoiceChat
{
    public class MicrophoneButtonController : IDisposable
    {
        private readonly List<MicrophoneButton> views;
        private readonly VoiceChatMicrophoneHandler voiceChatMicrophoneHandler;
        private IDisposable? microphoneStateSubscription;

        public MicrophoneButtonController(
            List<MicrophoneButton> views,
            VoiceChatMicrophoneHandler voiceChatMicrophoneHandler)
        {
            this.views = views;
            this.voiceChatMicrophoneHandler = voiceChatMicrophoneHandler;

            foreach (MicrophoneButton microphoneButton in views)
                microphoneButton.MicButton.onClick.AddListener(ToggleVoiceChat);

            microphoneStateSubscription = voiceChatMicrophoneHandler.IsMicrophoneEnabled.Subscribe(OnMicrophoneStateChanged);
        }

        private void ToggleVoiceChat()
        {
            voiceChatMicrophoneHandler.ToggleMicrophone();
        }

        private void OnMicrophoneStateChanged(bool isEnabled)
        {
            foreach (MicrophoneButton microphoneButton in views)
                microphoneButton.SetMicrophoneStatus(isEnabled);
        }

        public void Dispose()
        {
            microphoneStateSubscription?.Dispose();
            microphoneStateSubscription = null;
        }
    }
}
