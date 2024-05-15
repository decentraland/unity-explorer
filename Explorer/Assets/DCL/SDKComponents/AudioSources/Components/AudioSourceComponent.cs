using DCL.ECSComponents;
using System;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Serialization;
using Promise = ECS.StreamableLoading.Common.AssetPromise<UnityEngine.AudioClip, ECS.StreamableLoading.AudioClips.GetAudioClipIntention>;

namespace DCL.SDKComponents.AudioSources
{
    public struct AudioSourceComponent : IDisposable
    {
        public string AudioClipUrl;
        public Promise ClipPromise;

        /// <summary>
        ///     The final audio source ready for consumption
        /// </summary>
        public AudioSource AudioSource { get; private set; }
        public bool AudioSourceAssigned { get; private set; }

        public AudioSourceComponent(Promise promise, string audioClipUrl)
        {
            ClipPromise = promise;
            AudioClipUrl = audioClipUrl;
            Debug.LogError($"AUDIOCLIP - AudioSourceCreated - {promise} - {audioClipUrl}");

            AudioSource = null;
            AudioSourceAssigned = false;
        }

        public void SetAudioSource(AudioSource audioSource, AudioMixerGroup audioMixerGroup)
        {
            AudioSource = audioSource;
            Debug.LogError($"AUDIOCLIP - AudioSource Set {audioSource.clip}");

            if (audioMixerGroup != null) { audioSource.outputAudioMixerGroup = audioMixerGroup; }

            AudioSourceAssigned = true;
        }

        public void Dispose()
        {
            if (AudioSource != null)
                AudioSource.clip = null;

            AudioSource = null;
        }
    }
}
