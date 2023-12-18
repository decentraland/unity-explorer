using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.ECSComponents;
using DCL.Optimization.Pools;
using ECS.Abstract;
using ECS.LifeCycle.Components;
using UnityEngine;

namespace DCL.SDKComponents.AudioSources
{
    [UpdateInGroup(typeof(AudioSourceLoadingGroup))]
    [UpdateAfter(typeof(StartAudioSourceLoadingSystem))]
    public partial class CleanUpAudioSourceSystem: BaseUnityLoopSystem
    {
        private readonly IComponentPoolsRegistry poolsRegistry;

        internal CleanUpAudioSourceSystem(World world, IComponentPoolsRegistry poolsRegistry)  : base(world)
        {
            this.poolsRegistry = poolsRegistry;
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
            if(component.ClipLoadingStatus == ECS.StreamableLoading.LifeCycle.LoadingInProgress)
            {
                component.ClipPromise?.ForgetLoading(World);
                component.ClipPromise = null;

                return;
            }

            if (component.Result == null) return;

            // TODO: unparent on releasing
            component.Result.clip = null;
            if (poolsRegistry.TryGetPool(typeof(AudioSource), out IComponentPool componentPool))
                componentPool.Release(component.Result);
        }
    }
}
