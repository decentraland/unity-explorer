using System;
using DCL.ECSComponents;
using UnityEngine;
using UnityEngine.Audio;
using Plugins.NativeAudioAnalysis;
using Promise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.AudioClips.AudioClipData, ECS.StreamableLoading.AudioClips.GetAudioClipIntention>;

namespace DCL.SDKComponents.AudioSources
{
    public struct AudioSourceComponent : IDisposable, IComponentWithAudioFrameBuffer
    {
        public string AudioClipUrl;
        public Promise ClipPromise;

        /// <summary>
        ///     Tracks the last reported media state to avoid sending duplicate CRDT messages
        /// </summary>
        public MediaState LastPropagatedAudioState;

        /// <summary>
        ///     Last (clamped) CurrentTime seek target applied to the AudioSource. SDK7 AudioSource is a
        ///     whole-component LWW PUT, so CurrentTime is re-sent on every property change (e.g. volume);
        ///     comparing against this value distinguishes a genuine seek from a stale re-send and avoids
        ///     restarting already-playing audio. NaN means "no seek applied yet".
        /// </summary>
        public float LastAppliedCurrentTime;

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
        private ThreadSafeLastAudioFrameReadFilterWrap lastAudioFrameReadFilter;


        public AudioSourceComponent(Promise promise, string audioClipUrl)
        {
            ClipPromise = promise;
            AudioClipUrl = audioClipUrl;

            AudioSource = null;
            AudioSourceAssigned = false;
            LastPropagatedAudioState = MediaState.MsNone;
            LastAppliedCurrentTime = float.NaN;

            lastAudioFrameReadFilter = new ();
        }

        public void SetAudioSource(AudioSource audioSource, AudioMixerGroup audioMixerGroup)
        {
            AudioSource = audioSource;

            if (audioMixerGroup != null) { audioSource.outputAudioMixerGroup = audioMixerGroup; }

            AudioSourceAssigned = true;
        }

        public bool TryAttachLastAudioFrameReadFilterOrUseExisting(out ThreadSafeLastAudioFrameReadFilter? output) 
        {
            return lastAudioFrameReadFilter.TryAttachLastAudioFrameReadFilterOrUseExisting(AudioSource, out output);
        }

        public void EnsureLastAudioFrameReadFilterIsRemoved() 
        {
            lastAudioFrameReadFilter.EnsureLastAudioFrameReadFilterIsRemoved();
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
