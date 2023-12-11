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
            // entityTransform.Transform.
            Debug.Log($"VV: {sdkAudioSource.AudioClipUrl} {sdkAudioSource}");

            var audioSource = audioSourcesPool.Get();
            var component = new AudioSourceComponent();
            component.AudioSource = audioSource;

            // Instantiate(entity, crdtEntity, setupColliderCases[sdkComponent.MeshCase], ref component, ref sdkComponent, ref transform);
            World.Add(entity, component);
        }
    }
}
