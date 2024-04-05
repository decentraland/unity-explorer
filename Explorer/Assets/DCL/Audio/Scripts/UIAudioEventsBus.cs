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
        public event Action<AudioClipConfig, Vector3> PlayAudioAtPositionEvent;
        public event Action<AudioClipConfig, bool> PlayLoopingAudioEvent;

        public event Action<AudioClipConfig, AudioSource> PlayAudioWithAudioSourceEvent;

        public void Dispose() { }


        public void SendPlayAudioWithAudioSourceEvent(AudioClipConfig audioClipConfig, AudioSource audioSource)
        {
            if (audioClipConfig != null && audioSource != null) { PlayAudioWithAudioSourceEvent?.Invoke(audioClipConfig, audioSource); }
        }


        public void SendPlayAudioEvent(AudioClipConfig audioClipConfig)
        {
            if (audioClipConfig != null) { PlayAudioEvent?.Invoke(audioClipConfig); }
        }

        public void SendPlayAudioAtPositionEvent(AudioClipConfig audioClipConfig, Vector3 worldPosition)
        {
            if (audioClipConfig != null) { PlayAudioAtPositionEvent?.Invoke(audioClipConfig, worldPosition); }
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
