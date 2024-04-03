using System;
using UnityEngine;
using Random = UnityEngine.Random;

namespace DCL.Audio
{
    public class UIAudioManagerContainer : MonoBehaviour, IDisposable
    {
        [SerializeField]
        private AudioSource audioSource;

        [SerializeField]
        private UIAudioSettings UIAudioSettings;

        public void Dispose()
        {
            UIAudioEventsBus.Instance.AudioEvent -= OnAudioEvent;
            audioSource.Stop();
        }

        public void Initialize()
        {
            UIAudioEventsBus.Instance.AudioEvent += OnAudioEvent;
        }

        private void OnAudioEvent(AudioClipConfig audioClipConfig)
        {
            if (UIAudioSettings.UIAudioVolume > 0) //Here we will use proper Settings for the type of audio clip
            {
                if (audioClipConfig.audioClips.Length > 1)
                {
                    int randomIndex = Random.Range(0, audioClipConfig.audioClips.Length);
                    AudioClip randomClip = audioClipConfig.audioClips[randomIndex];
                    audioSource.PlayOneShot(randomClip, UIAudioSettings.UIAudioVolume);
                }
            }
        }
    }
}
