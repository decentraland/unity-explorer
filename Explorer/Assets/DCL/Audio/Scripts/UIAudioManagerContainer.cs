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
        [FormerlySerializedAs("audioSource")]
        [SerializeField]
        private AudioSource AudioSource;
        [SerializeField]
        private UIAudioSettings UIAudioSettings;
        [SerializeField]
        private float fadeDuration = 1.5f;
        [SerializeField]
        private AudioSettings audioSettings;

        private Dictionary<AudioCategory,List<AudioSource>> currentAudioSources = new Dictionary<AudioCategory, List<AudioSource>>();
        private IComponentPool<AudioSource> audioSourcePool;

        public void Dispose()
        {
            UIAudioEventsBus.Instance.PlayAudioEvent -= OnPlayAudioEvent;
            AudioSource.Stop();
            //Dispose of IEnumerators
        }

        public void Initialize(IComponentPool<AudioSource> audioSourcePool)
        {
            this.audioSourcePool = audioSourcePool;
            UIAudioEventsBus.Instance.PlayAudioEvent += OnPlayAudioEvent;
            UIAudioEventsBus.Instance.PlayLoopingAudioEvent += OnPlayLoopingAudioEvent;
            UIAudioEventsBus.Instance.PlayAudioWithAudioSourceEvent += OnPlayAudioWithAudioSourceEvent;
        }

        private void OnPlayLoopingAudioEvent(AudioClipConfig audioClipConfig, bool startLoop)
        {
            if (UIAudioSettings.UIAudioVolume > 0) // Check if audio volume is greater than 0
            {
                if (startLoop)
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
                AudioSource.volume = Mathf.Lerp(startVolume, targetVolume, currentTime / duration);
                yield return null;
            }
            AudioSource.volume = targetVolume;
            AudioSource.clip = clip;
            AudioSource.loop = true;
            AudioSource.Play();
        }

        private IEnumerator FadeOutAndStop(float duration)
        {
            float currentTime = 0;
            float startVolume = AudioSource.volume;
            while (currentTime < duration)
            {
                currentTime += Time.deltaTime;
                AudioSource.volume = Mathf.Lerp(startVolume, 0, currentTime / duration);
                yield return null;
            }
            AudioSource.volume = 0;
            AudioSource.loop = false;
            AudioSource.Stop();
        }


        //We will need to use a pooled AudioSource and set proper volume, priority, pitch, volume, etc for each sound depending on category

        private void OnPlayAudioWithAudioSourceEvent(AudioClipConfig audioClipConfig, AudioSource audioSource)
        {
            int clipIndex = AudioPlaybackUtilities.GetClipIndex(audioClipConfig);
            PlayClip(audioClipConfig, audioSource, clipIndex);
        }

        private void PlayClip(AudioClipConfig audioClipConfig, AudioSource audioSource, int clipIndex)
        {
            audioSource.pitch = 1 + AudioPlaybackUtilities.GetPitchVariation(audioClipConfig);

            if (audioClipConfig.ClipPlaybackMode == AudioClipPlaybackMode.Once)
            {
                audioSource.PlayOneShot(audioClipConfig.audioClips[clipIndex], audioClipConfig.relativeVolume);
            }
            else if (audioClipConfig.ClipPlaybackMode == AudioClipPlaybackMode.Loop)
            {
                audioSource.Stop(); //Maybe do fadeout and fade in?
                audioSource.clip = audioClipConfig.audioClips[clipIndex];

                switch (audioClipConfig.ClipLoopMode)
                {
                    case AudioClipLoopMode.Loop:
                        audioSource.loop = true;
                        break;
                    case AudioClipLoopMode.Contiguous:
                        break;
                    case AudioClipLoopMode.Random:
                        break;
                    default: throw new ArgumentOutOfRangeException();
                }

                audioSource.Play();
            }
        }


        private void PlayLoop(AudioClipConfig audioClipConfig, AudioSource audioSource, AudioClip clip)
        {
            audioSource.Stop();
            audioSource.loop = true;
            audioSource.clip = clip;
            audioSource.Play();

        }

        private AudioMixerGroup GetAudioMixerGroup(AudioClipConfig audioClipConfig)
        {
            return audioSettings.CategorySettings.Find(c => c.key == audioClipConfig.audioCategory).value.audioMixerGroup;
        }


        private void OnPlayAudioEvent(AudioClipConfig audioClipConfig)
        {
            if (UIAudioSettings.UIAudioVolume > 0) //Here we will use proper Settings for the type of audio clip
            {
                AudioSource.volume = 1;

                if (audioClipConfig.audioClips.Length > 1)
                {
                    int randomIndex = Random.Range(0, audioClipConfig.audioClips.Length);
                    AudioClip randomClip = audioClipConfig.audioClips[randomIndex];
                    AudioSource.PlayOneShot(randomClip, UIAudioSettings.UIAudioVolume * audioClipConfig.relativeVolume);
                }
                else
                {
                    AudioSource.PlayOneShot(audioClipConfig.audioClips[0], UIAudioSettings.UIAudioVolume * audioClipConfig.relativeVolume);
                }
            }
        }
    }
}
