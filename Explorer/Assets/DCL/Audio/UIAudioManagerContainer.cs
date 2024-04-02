using System;
using UnityEngine;

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

        private void OnAudioEvent(UIAudioType audioType)
        {
            if (UIAudioSettings.UIAudioVolume > 0) { audioSource.PlayOneShot(UIAudioSettings.GetAudioClipForType(audioType), UIAudioSettings.UIAudioVolume); }
        }
    }
}
