using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Optimization.Pools;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle;
using ECS.LifeCycle.Components;
using ECS.StreamableLoading.AudioClips;
using UnityEngine;

namespace DCL.SDKComponents.AudioSources
{
    [UpdateInGroup(typeof(CleanUpGroup))]
    [LogCategory(ReportCategory.SDK_AUDIO_SOURCES)]
    [ThrottlingEnabled]
    public partial class CleanUpAudioSourceSystem : BaseUnityLoopSystem, IFinalizeWorldSystem
    {
        private ReleaseAudioSourceComponent releaseAudioSourceComponent;

        private CleanUpAudioSourceSystem(World world, AudioClipsCache cache, IComponentPoolsRegistry poolsRegistry) : base(world)
        {
            releaseAudioSourceComponent = new ReleaseAudioSourceComponent(world, cache, poolsRegistry);
        }

        protected override void Update(float t)
        {
            HandleEntityDestructionQuery(World);
            HandleComponentRemovalQuery(World);

            World.Remove<AudioSourceComponent>(in HandleComponentRemoval_QueryDescription);
        }

        [Query]
        [None(typeof(PBAudioSource), typeof(DeleteEntityIntention))]
        private void HandleComponentRemoval(ref AudioSourceComponent component)
        {
            releaseAudioSourceComponent.Update(ref component);
        }

        [Query]
        [All(typeof(DeleteEntityIntention))]
        private void HandleEntityDestruction(ref AudioSourceComponent component)
        {
            releaseAudioSourceComponent.Update(ref component);
        }

        public void FinalizeComponents(in Query query)
        {
            World.InlineQuery<ReleaseAudioSourceComponent, AudioSourceComponent>(in new QueryDescription().WithAll<AudioSourceComponent>(), ref releaseAudioSourceComponent);
        }

        private readonly struct ReleaseAudioSourceComponent : IForEach<AudioSourceComponent>
        {
            private readonly World world;
            private readonly AudioClipsCache cache;
            private readonly IComponentPool componentPool;

            public ReleaseAudioSourceComponent(World world, AudioClipsCache cache, IComponentPoolsRegistry poolsRegistry)
            {
                this.world = world;
                this.cache = cache;

                poolsRegistry.TryGetPool(typeof(AudioSource), out componentPool);
            }

            public void Update(ref AudioSourceComponent component)
            {
                component.CleanUp(world, cache, componentPool);
                componentPool.Release(component.AudioSource);

                component.Dispose();
            }
        }
    }
}
