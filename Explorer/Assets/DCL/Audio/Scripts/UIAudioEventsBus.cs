using DCL.Diagnostics;
using System;
using UnityEngine;

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

        public event Action<AudioClipConfig> PlayAudioEvent;
        public event Action<AudioClipConfig, bool> PlayLoopingAudioEvent;

        public void Dispose() { }


        public void SendPlayAudioEvent(AudioClipConfig audioClipConfig)
        {
            if (audioClipConfig != null) { PlayAudioEvent?.Invoke(audioClipConfig); }
        }

        public void SendPlayLoopingAudioEvent(AudioClipConfig audioClipConfig)
        {
            if (audioClipConfig != null) { PlayLoopingAudioEvent?.Invoke(audioClipConfig, true); }
        }
        public void SendStopPlayingLoopingAudioEvent(AudioClipConfig audioClipConfig)
        {
            if (audioClipConfig != null) { PlayLoopingAudioEvent?.Invoke(audioClipConfig, false); }
        }

    }
}
