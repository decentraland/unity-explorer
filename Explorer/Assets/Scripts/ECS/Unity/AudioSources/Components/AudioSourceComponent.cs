using DCL.ECSComponents;
using DCL.Optimization.Pools;
using SceneRunner.Scene;
using System;
using UnityEngine;
using Promise = ECS.StreamableLoading.Common.AssetPromise<UnityEngine.AudioClip, ECS.StreamableLoading.AudioClips.GetAudioClipIntention>;

namespace ECS.Unity.AudioSources.Components
{
    public struct AudioSourceComponent: IPoolableComponentProvider<AudioSource>
    {
        public PBAudioSource PBAudioSource;

        public Promise? ClipPromise;

        /// <summary>
        ///     The current status of the Audio Clip loading
        /// </summary>
        public StreamableLoading.LifeCycle ClipLoadingStatus;

        /// <summary>
        ///     The final material ready for consumption
        /// </summary>
        public AudioSource Result;
        public AudioSource PoolableComponent => Result;

        AudioSource IPoolableComponentProvider<AudioSource>.PoolableComponent => Result;
        Type IPoolableComponentProvider<AudioSource>.PoolableComponentType => typeof(AudioSource);

        public AudioSourceComponent(PBAudioSource pbAudioSource, ISceneData data)
        {
            ClipPromise = null;

            PBAudioSource = pbAudioSource;
            ClipLoadingStatus = StreamableLoading.LifeCycle.LoadingNotStarted;
            Result = null;
        }

        public void Dispose(){ }

    }
}
