using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Optimization.Pools;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle.Components;
using ECS.StreamableLoading;
using ECS.StreamableLoading.AudioClips;
using System;
using UnityEngine;

namespace DCL.SDKComponents.AudioSources
{
    [UpdateInGroup(typeof(CleanUpGroup))]
    [LogCategory(ReportCategory.AUDIO_SOURCES)]
    [ThrottlingEnabled]
    public partial class CleanUpAudioSourceSystem: BaseUnityLoopSystem
    {
        private readonly IComponentPoolsRegistry poolsRegistry;
        private readonly AudioClipsCache cache;

        internal CleanUpAudioSourceSystem(World world,  AudioClipsCache cache,IComponentPoolsRegistry poolsRegistry)  : base(world)
        {
            this.poolsRegistry = poolsRegistry;
            this.cache = cache;
        }

        protected override void Update(float t)
        {
            // TODO: ref/deref clips in cache
            HandleEntityDestructionQuery(World);
            HandleComponentRemovalQuery(World);

            World.Remove<AudioSourceComponent>(in HandleComponentRemoval_QueryDescription);
        }

        [Query]
        [None(typeof(PBAudioSource), typeof(DeleteEntityIntention))]
        private void HandleComponentRemoval(ref AudioSourceComponent component)
        {
            RemoveComponent(ref component);
        }

        [Query]
        [All(typeof(DeleteEntityIntention))]
        private void HandleEntityDestruction(ref AudioSourceComponent component)
        {
            RemoveComponent(ref component);
        }

        private void RemoveComponent(ref AudioSourceComponent component)
        {
            switch (component.ClipLoadingStatus)
            {
                case LifeCycle.LoadingNotStarted: return;
                case LifeCycle.LoadingInProgress:
                    component.ClipPromise?.ForgetLoading(World);
                    component.ClipPromise = null;
                    return;
            }

            if (component.Result == null) return;

            cache.Dereference(component.ClipPromise!.Value.LoadingIntention, component.Result.clip);

            component.Result.clip = null;
            if (poolsRegistry.TryGetPool(typeof(AudioSource), out IComponentPool componentPool))
                componentPool.Release(component.Result);
        }
    }
}
