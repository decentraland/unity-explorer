using System;
using UnityEngine;
using UnityEngine.Audio;
using Plugins.NativeAudioAnalysis;
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

        /// <summary>
        ///     Use ThreadSafeLastAudioFrameReadFilter because it has to be attached to the same GameObject.
        ///     But GameObject is owned by AudioSource MonoBehavour in practice, and gets repooled with it.
        ///     To avoid LifeCycle complications ThreadSafeLastAudioFrameReadFilter is referenced directly and owned by AudioSourceComponent.
        ///     MonoBehaviour cannot be easily pooled because the ownership issue arise. 
        ///     AudioSource and ThreadSafeLastAudioFrameReadFilter share the same GameObject.
        /// </summary>
        private ThreadSafeLastAudioFrameReadFilter? lastAudioFrameReadFilter;


        public AudioSourceComponent(Promise promise, string audioClipUrl)
        {
            ClipPromise = promise;
            AudioClipUrl = audioClipUrl;

            AudioSource = null;
            AudioSourceAssigned = false;

            lastAudioFrameReadFilter = null;
        }

        public void SetAudioSource(AudioSource audioSource, AudioMixerGroup audioMixerGroup)
        {
            AudioSource = audioSource;

            if (audioMixerGroup != null) { audioSource.outputAudioMixerGroup = audioMixerGroup; }

            AudioSourceAssigned = true;
        }

        public bool TryAttachLastAudioFrameReadFilterOrUseExisting(out ThreadSafeLastAudioFrameReadFilter output) 
        {
            if (lastAudioFrameReadFilter != null)
            {
                output = lastAudioFrameReadFilter;
                return true;
            }


            if (AudioSource != null) 
            {
                output = lastAudioFrameReadFilter = AudioSource.gameObject.AddComponent<ThreadSafeLastAudioFrameReadFilter>();
                return lastAudioFrameReadFilter != null;
            }

            output = null;
            return false;
        }


        public void EnsureLastAudioFrameReadFilterIsRemoved() 
        {
            if (lastAudioFrameReadFilter != null) 
            {
                // Can be pooled
                UnityEngine.Object.Destroy(lastAudioFrameReadFilter);
                lastAudioFrameReadFilter = null;
            }
        }

        public void Dispose()
        {
            if (AudioSource != null)
                AudioSource.clip = null;

            AudioSource = null;
            EnsureLastAudioFrameReadFilterIsRemoved();
        }
    }
}
