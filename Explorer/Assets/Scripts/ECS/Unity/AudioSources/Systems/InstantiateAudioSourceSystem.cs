using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Optimization.Pools;
using ECS.Abstract;
using ECS.Unity.AudioSources.Components;
using ECS.Unity.Groups;
using ECS.Unity.Transforms.Components;
using UnityEngine;
using Utility;

namespace ECS.Unity.AudioSources.Systems
{
    [UpdateInGroup(typeof(ComponentInstantiationGroup))]
    [LogCategory(ReportCategory.AUDIO_SOURCES)]
    public partial class InstantiateAudioSourceSystem : BaseUnityLoopSystem
    {
        private readonly IComponentPool<AudioSource> audioSourcesPool;

        internal InstantiateAudioSourceSystem(World world, IComponentPoolsRegistry poolsRegistry) : base(world)
        {
            audioSourcesPool = poolsRegistry.GetReferenceTypePool<AudioSource>();
        }

        protected override void Update(float t)
        {
            InstantiateAudioSourceQuery(World);
        }

        [Query]
        [All(typeof(PBAudioSource), typeof(TransformComponent))]
        [None(typeof(AudioSourceComponent))]
        private void InstantiateAudioSource(in Entity entity, ref PBAudioSource sdkAudioSource, ref TransformComponent entityTransform)
        {
            // Debug.Log($"VV: {assetBundleResult.Asset}");

            AudioSource audioSource = audioSourcesPool.Get();

            audioSource.loop = sdkAudioSource.Loop;
            audioSource.pitch = sdkAudioSource.Pitch;
            audioSource.volume = sdkAudioSource.Volume;

            audioSource.playOnAwake = false;
            if (sdkAudioSource.Playing && audioSource.clip != null)
                audioSource.Play();

            // sdkAudioSource.AudioClipUrl;

            var component = new AudioSourceComponent();
            component.AudioSource = audioSource;

            Transform rendererTransform = audioSource.transform;
            rendererTransform.SetParent(entityTransform.Transform, false);
            rendererTransform.ResetLocalTRS();

            World.Add(entity, component);
        }
    }
}
