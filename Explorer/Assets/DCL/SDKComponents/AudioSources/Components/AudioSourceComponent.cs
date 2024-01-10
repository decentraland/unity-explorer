using DCL.ECSComponents;
using DCL.Optimization.Pools;
using System;
using UnityEngine;
using Promise = ECS.StreamableLoading.Common.AssetPromise<UnityEngine.AudioClip, ECS.StreamableLoading.AudioClips.GetAudioClipIntention>;

namespace DCL.SDKComponents.AudioSources
{
    public struct AudioSourceComponent : IDisposable
    {
        public readonly PBAudioSource PBAudioSource;

        public Promise? ClipPromise;

        /// <summary>
        ///     The final audio source ready for consumption
        /// </summary>
        public AudioSource Result;

        public bool ClipIsNotLoading => ClipPromise == null;
        public bool ClipLoadingFinished => ClipPromise != null && Result != null;

        public AudioSourceComponent(PBAudioSource pbAudioSource)
        {
            ClipPromise = null;
            PBAudioSource = pbAudioSource;
            Result = null;
        }

        public void Dispose()
        {
            Result.clip = null;
        }
    }
}
