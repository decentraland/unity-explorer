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
        public readonly PBAudioSource PBAudioSource;
        public string AudioClipUrl;
        public Promise ClipPromise;

        /// <summary>
        ///     The final audio source ready for consumption
        /// </summary>
        public AudioSource AudioSource { get; private set; }
        public bool AudioSourceAssigned { get; private set;}

        public AudioSourceComponent(PBAudioSource pbAudioSource, Promise promise)
        {
            ClipPromise = promise;
            PBAudioSource = pbAudioSource;
            AudioClipUrl = pbAudioSource.AudioClipUrl;

            AudioSource = null;
            AudioSourceAssigned = false;
        }

        public void SetAudioSource(AudioSource audioSource, AudioMixerGroup audioMixerGroup)
        {
            AudioSource = audioSource;
            if (audioMixerGroup != null) { audioSource.outputAudioMixerGroup = audioMixerGroup; }
            audioSource.spatialBlend = 1; // We make the AudioSource to work on 3D space
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
