using DCL.ECSComponents;
using DCL.Optimization.Pools;
using System;
using UnityEngine;
using Promise = ECS.StreamableLoading.Common.AssetPromise<UnityEngine.AudioClip, ECS.StreamableLoading.AudioClips.GetAudioClipIntention>;

namespace DCL.SDKComponents.AudioSources
{
    public struct AudioSourceComponent : IPoolableComponentProvider<AudioSource?>
    {
        public readonly PBAudioSource PBAudioSource;

        public Promise? ClipPromise;

        /// <summary>
        ///     The current status of the Audio Clip loading
        /// </summary>
        public ECS.StreamableLoading.LifeCycle ClipLoadingStatus;

        /// <summary>
        ///     The final material ready for consumption
        /// </summary>
        public AudioSource? Result;
        public AudioSource? PoolableComponent => Result;

        AudioSource? IPoolableComponentProvider<AudioSource>.PoolableComponent => Result;
        Type IPoolableComponentProvider<AudioSource?>.PoolableComponentType => typeof(AudioSource);

        public bool ClipIsNotLoading => ClipLoadingStatus != ECS.StreamableLoading.LifeCycle.LoadingInProgress;

        public AudioSourceComponent(PBAudioSource pbAudioSource)
        {
            ClipPromise = null;

            PBAudioSource = pbAudioSource;
            ClipLoadingStatus = ECS.StreamableLoading.LifeCycle.LoadingNotStarted;
            Result = null;
        }

        public void Dispose() { }
    }
}
