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
    [LogCategory(ReportCategory.AUDIO_SOURCES)]
    [ThrottlingEnabled]
    public partial class CleanUpAudioSourceSystem: BaseUnityLoopSystem, IFinalizeWorldSystem
    {
        private ReleaseAudioSourceComponent releaseAudioSourceComponent;

        private CleanUpAudioSourceSystem(World world, AudioClipsCache cache,IComponentPoolsRegistry poolsRegistry)  : base(world)
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
            private readonly IComponentPoolsRegistry poolsRegistry;
            private readonly AudioClipsCache cache;

            public ReleaseAudioSourceComponent(World world, AudioClipsCache cache,IComponentPoolsRegistry poolsRegistry)
            {
                this.world = world;
                this.cache = cache;
                this.poolsRegistry = poolsRegistry;
            }

            public void Update(ref AudioSourceComponent component)
            {
                if (component.ClipPromise == null) return; // loading not started

                if (component.Result == null) // loading in progress
                {
                    component.ClipPromise.Value.ForgetLoading(world);
                    component.ClipPromise = null;
                    return;
                }

                cache.Dereference(component.ClipPromise!.Value.LoadingIntention, component.Result.clip);

                if (poolsRegistry.TryGetPool(typeof(AudioSource), out IComponentPool componentPool))
                    componentPool.Release(component.Result);

                component.Dispose();
            }
        }
    }
}
