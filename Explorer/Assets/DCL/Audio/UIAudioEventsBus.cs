using System;

namespace DCL.Audio
{
    public class UIAudioEventsBus : IDisposable
    {
        private static UIAudioEventsBus instance;

        public static UIAudioEventsBus Instance => instance ??= new UIAudioEventsBus();

        public event Action<AudioClipConfig> PlayUIAudioEvent;
        public event Action<AudioClipConfig> PlayContinuousUIAudioEvent;
        public event Action<AudioClipConfig> StopContinuousUIAudioEvent;

        public void Dispose() { }

        public void SendPlayAudioEvent(AudioClipConfig audioClipConfig)
        {
            if (audioClipConfig != null)
                PlayUIAudioEvent?.Invoke(audioClipConfig);
        }

        public void SendPlayContinuousAudioEvent(AudioClipConfig audioClipConfig)
        {
            if (audioClipConfig != null)
                PlayContinuousUIAudioEvent?.Invoke(audioClipConfig);
        }

        public void SendStopPlayingContinuousAudioEvent(AudioClipConfig audioClipConfig)
        {
            if (audioClipConfig != null)
                StopContinuousUIAudioEvent?.Invoke(audioClipConfig);
        }
    }
}
