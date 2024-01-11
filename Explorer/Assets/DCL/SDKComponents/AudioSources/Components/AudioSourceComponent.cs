using DCL.ECSComponents;
using System;
using UnityEngine;
using Promise = ECS.StreamableLoading.Common.AssetPromise<UnityEngine.AudioClip, ECS.StreamableLoading.AudioClips.GetAudioClipIntention>;

namespace DCL.SDKComponents.AudioSources
{
    public struct AudioSourceComponent : IDisposable
    {
        public readonly PBAudioSource PBAudioSource;

        public Promise ClipPromise;

        /// <summary>
        ///     The final audio source ready for consumption
        /// </summary>
        public AudioSource Result;

        public AudioSourceComponent(PBAudioSource pbAudioSource, Promise promise)
        {
            ClipPromise = promise;
            PBAudioSource = pbAudioSource;
            Result = null;
        }

        public void Dispose()
        {
            Result.clip = null;
        }
    }
}
