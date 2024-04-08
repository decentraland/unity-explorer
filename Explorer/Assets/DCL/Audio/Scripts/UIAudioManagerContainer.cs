using DCL.Optimization.Pools;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Serialization;
using Random = UnityEngine.Random;

namespace DCL.Audio
{
    public class UIAudioManagerContainer : MonoBehaviour, IDisposable
    {
        [SerializeField]
        private AudioSource UiAudioSource;
        [SerializeField]
        private AudioSource MusicAudioSource;
        [SerializeField]
        private AudioSource ChatAudioSource;
        [SerializeField]
        private float fadeDuration = 1.5f;//This should go into music settings or just general settings
        [SerializeField]
        private AudioSettings audioSettings;

        private Dictionary<AudioCategory,List<AudioSource>> currentAudioSources = new Dictionary<AudioCategory, List<AudioSource>>();
        private IComponentPool<AudioSource> audioSourcePool;

        public void Dispose()
        {
            UIAudioEventsBus.Instance.PlayAudioEvent -= OnPlayAudioEvent;
            UiAudioSource.Stop();
            MusicAudioSource.Stop();
            ChatAudioSource.Stop();
        }

        public void Initialize()
        {
            UIAudioEventsBus.Instance.PlayAudioEvent += OnPlayAudioEvent;
            UIAudioEventsBus.Instance.PlayLoopingAudioEvent += OnPlayLoopingAudioEvent;
        }

        private void OnPlayLoopingAudioEvent(AudioClipConfig audioClipConfig, bool startLoop)
        {
            //if (UIAudioSettings.UIAudioVolume > 0) // Check if audio volume is greater than 0
            {
                if (startLoop)
                {
                    if (audioClipConfig.AudioClips.Length > 0)
                    {
                        int randomIndex = Random.Range(0, audioClipConfig.AudioClips.Length);
                        AudioClip randomClip = audioClipConfig.AudioClips[randomIndex];
                   //     StartCoroutine(FadeInAndPlay(randomClip, UIAudioSettings.UIAudioVolume * audioClipConfig.relativeVolume, fadeDuration));
                    }
                }
                else
                {
                //    StartCoroutine(FadeOutAndStop(fadeDuration));
                }
            }
        }


        private IEnumerator FadeInAndPlay(AudioClip clip, float targetVolume, float duration, AudioSource audioSource)
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

        private IEnumerator FadeOutAndStop(float duration, AudioSource audioSource)
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
            if (audioClipConfig.Category is not (AudioCategory.Chat or AudioCategory.Music or AudioCategory.UI))
                //We can only play UI sounds through this bus. Other sounds are discarded -> maybe add a log here?
                return;

            var settings = audioSettings.GetSettingsForCategory(audioClipConfig.Category);
            if (!settings.AudioEnabled) return;

            int clipIndex = AudioPlaybackUtilities.GetClipIndex(audioClipConfig);

            var audioSource = GetAudioSourceForCategory(audioClipConfig.Category);

            audioSource.pitch = AudioPlaybackUtilities.GetPitchWithVariation(audioClipConfig);
            audioSource.PlayOneShot(audioClipConfig.AudioClips[clipIndex], audioClipConfig.RelativeVolume);
        }

        private AudioSource GetAudioSourceForCategory(AudioCategory audioCategory)
        {
            switch (audioCategory)
            {
                case AudioCategory.UI: return UiAudioSource;
                case AudioCategory.Chat: return ChatAudioSource;
                case AudioCategory.Music: return MusicAudioSource;
                case AudioCategory.World:
                case AudioCategory.Avatar:
                case AudioCategory.None:
                default: throw new ArgumentOutOfRangeException(nameof(audioCategory), audioCategory, null);
            }
        }
    }
}
