using System;

namespace DCL.Audio
{
    public class UIAudioEventsBus : IDisposable
    {
        private static UIAudioEventsBus instance;

        public static UIAudioEventsBus Instance
        {
            get
            {
                return instance ??= new UIAudioEventsBus();
            }
        }

        public event Action<AudioClipConfig> PlayUIAudioEvent;
        public event Action<AudioClipConfig, bool> PlayLoopingUIAudioEvent;


        public void Dispose() { }

        public void SendPlayAudioEvent(AudioClipConfig audioClipConfig)
        {
            if (audioClipConfig != null) { PlayUIAudioEvent?.Invoke(audioClipConfig); }
        }

        public void SendPlayLoopingAudioEvent(AudioClipConfig audioClipConfig)
        {
            if (audioClipConfig != null) { PlayLoopingUIAudioEvent?.Invoke(audioClipConfig, true); }
        }

        public void SendStopPlayingLoopingAudioEvent(AudioClipConfig audioClipConfig)
        {
            if (audioClipConfig != null) { PlayLoopingUIAudioEvent?.Invoke(audioClipConfig, false); }
        }
    }
}
