using System;
using System.Collections;
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

        [SerializeField]
        private float fadeDuration = 1.5f;


        public void Dispose()
        {
            AudioEventsBus.Instance.PlayAudioEvent -= OnPlayAudioEvent;
            audioSource.Stop();
            //Dispose of IEnumerators
        }

        public void Initialize()
        {
            AudioEventsBus.Instance.PlayAudioEvent += OnPlayAudioEvent;
            AudioEventsBus.Instance.PlayLoopingAudioEvent += OnPlayLoopingAudioEvent;
        }

        private void OnPlayLoopingAudioEvent(AudioClipConfig audioClipConfig, bool loop)
        {
            if (UIAudioSettings.UIAudioVolume > 0) // Check if audio volume is greater than 0
            {
                if (loop)
                {
                    if (audioClipConfig.audioClips.Length > 0)
                    {
                        int randomIndex = Random.Range(0, audioClipConfig.audioClips.Length);
                        AudioClip randomClip = audioClipConfig.audioClips[randomIndex];
                        StartCoroutine(FadeInAndPlay(randomClip, UIAudioSettings.UIAudioVolume * audioClipConfig.relativeVolume, fadeDuration));
                    }
                }
                else
                {
                    StartCoroutine(FadeOutAndStop(fadeDuration));
                }
            }
        }


        private IEnumerator FadeInAndPlay(AudioClip clip, float targetVolume, float duration)
        {
            float currentTime = 0;
            float startVolume = 0;
            while (currentTime < duration)
            {
                currentTime += Time.deltaTime;
                audioSource.volume = Mathf.Lerp(startVolume, targetVolume, currentTime / duration);
                yield return null;
            }
            audioSource.volume = targetVolume;
            audioSource.clip = clip;
            audioSource.loop = true;
            audioSource.Play();
        }

        private IEnumerator FadeOutAndStop(float duration)
        {
            float currentTime = 0;
            float startVolume = audioSource.volume;
            while (currentTime < duration)
            {
                currentTime += Time.deltaTime;
                audioSource.volume = Mathf.Lerp(startVolume, 0, currentTime / duration);
                yield return null;
            }
            audioSource.volume = 0;
            audioSource.loop = false;
            audioSource.Stop();
        }


        //We will need to use a pooled AudioSource and set proper volume, priority, pitch, volume, etc for each sound depending on category


        private void OnPlayAudioEvent(AudioClipConfig audioClipConfig)
        {
            if (UIAudioSettings.UIAudioVolume > 0) //Here we will use proper Settings for the type of audio clip
            {
                audioSource.volume = 1;

                if (audioClipConfig.audioClips.Length > 1)
                {
                    int randomIndex = Random.Range(0, audioClipConfig.audioClips.Length);
                    AudioClip randomClip = audioClipConfig.audioClips[randomIndex];
                    audioSource.PlayOneShot(randomClip, UIAudioSettings.UIAudioVolume * audioClipConfig.relativeVolume);
                }
                else
                {
                    audioSource.PlayOneShot(audioClipConfig.audioClips[0], UIAudioSettings.UIAudioVolume * audioClipConfig.relativeVolume);
                }
            }
        }
    }
}
