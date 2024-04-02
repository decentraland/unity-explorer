using System;
using UnityEngine;

namespace DCL.Audio
{
    public class UIAudioManagerContainer: MonoBehaviour, IDisposable
    {
        [SerializeField]
        private AudioSource audioSource;

        [SerializeField]
        private UIAudioSettings UIAudioSettings;

        private IUIAudioEventsBus uiAudioEventsBus;


        public void Setup(IUIAudioEventsBus uiAudioEventsBus)
        {
            this.uiAudioEventsBus = uiAudioEventsBus;
            uiAudioEventsBus.AudioEvent += OnAudioEvent;
        }

        private void OnAudioEvent(UIAudioType audioType)
        {
            audioSource.PlayOneShot(UIAudioSettings.GetAudioClipForType(audioType));
        }

        public void Dispose()
        {
            uiAudioEventsBus.AudioEvent -= OnAudioEvent;
            audioSource.Stop();
        }
    }
}
