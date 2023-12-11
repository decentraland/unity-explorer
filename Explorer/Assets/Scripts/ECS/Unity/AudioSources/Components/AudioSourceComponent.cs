using DCL.Optimization.Pools;
using System;
using UnityEngine;

namespace ECS.Unity.AudioSources.Components
{
    public struct AudioSourceComponent: IPoolableComponentProvider<AudioSource>
    {
        public AudioSource AudioSource;
        public AudioSource PoolableComponent => AudioSource;

        AudioSource IPoolableComponentProvider<AudioSource>.PoolableComponent => AudioSource;
        Type IPoolableComponentProvider<AudioSource>.PoolableComponentType => typeof(AudioSource);

        public void Dispose(){ }

    }
}
