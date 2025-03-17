﻿using System;
using UnityEngine;
using UnityEngine.Audio;
using Promise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.AudioClips.AudioClipData, ECS.StreamableLoading.AudioClips.GetAudioClipIntention>;

namespace DCL.SDKComponents.AudioSources
{
    public struct AudioSourceComponent : IDisposable
    {
        public string AudioClipUrl;
        public Promise ClipPromise;

        /// <summary>
        ///     The final audio source ready for consumption
        /// </summary>
        public AudioSource? AudioSource { get; private set; }
        public bool AudioSourceAssigned { get; private set; }

        public AudioSourceComponent(Promise promise, string audioClipUrl)
        {
            ClipPromise = promise;
            AudioClipUrl = audioClipUrl;

            AudioSource = null;
            AudioSourceAssigned = false;
        }

        public void SetAudioSource(AudioSource audioSource, AudioMixerGroup audioMixerGroup)
        {
            AudioSource = audioSource;

            if (audioMixerGroup != null) { audioSource.outputAudioMixerGroup = audioMixerGroup; }

            AudioSourceAssigned = true;
        }

        /// <summary>
        /// Sets the mute property of the Unity's AudioSource, if it was loaded.
        /// </summary>
        /// <param name="mute">The value to replace AudioSource's.</param>
        public void Mute(bool mute)
        {
            if (AudioSource != null)
            {
                AudioSource.mute = mute;
            }
        }

        public void Dispose()
        {
            if (AudioSource != null)
                AudioSource.clip = null;

            AudioSource = null;
        }
    }
}
