using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.SDKComponents.Animator.Components;
using DCL.SDKComponents.Animator.Extensions;
using ECS.Abstract;
using ECS.LifeCycle.Components;
using ECS.Unity.GLTFContainer.Components;
using ECS.Unity.Groups;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.SDKComponents.Animator.Systems
{
    [UpdateInGroup(typeof(ComponentInstantiationGroup))]
    [LogCategory(ReportCategory.ANIMATOR)]
    [ThrottlingEnabled]
    public partial class AnimatorHandlerSystem : BaseUnityLoopSystem
    {
        public AnimatorHandlerSystem(World world) : base(world) { }

        protected override void Update(float t)
        {
            LoadAnimatorQuery(World!);
            UpdateAnimationStateQuery(World!);
            HandleComponentRemovalQuery(World!);
            World!.Remove<AnimationLoadedComponent>(in HandleComponentRemoval_QueryDescription);
        }

        [Query]
        [None(typeof(AnimationLoadedComponent))]
        private void LoadAnimator(in Entity entity, ref GltfContainerComponent gltfContainerComponent)
        {
            if (GltfReady(gltfContainerComponent) == false)
                return;

            foreach (Animation animation in gltfContainerComponent.Promise.Result.Value.Asset.Animations)
                animation.Initialize();

            World.Add(entity, new AnimationLoadedComponent());
        }

        //Until the GLTF Container is not fully loaded (and it has at least one animation) we do not create the SDKAnimator
        private static bool GltfReady(GltfContainerComponent gltfContainerComponent)
        {
            if (gltfContainerComponent.State.Value != LoadingState.Finished) return false;
            if (gltfContainerComponent.Promise is not { Result: { } }) return false;
            if (gltfContainerComponent.Promise.Result.Value.Asset?.Animations.Count == 0) return false;

            return true;
        }

        [Query]
        [All(typeof(AnimationLoadedComponent))]
        private void UpdateAnimationState(ref PBAnimator pbAnimator, ref GltfContainerComponent gltfContainerComponent)
        {
            //if PBAnimator is dirty, we need to update the SDKAnimatorComponent

            if (pbAnimator.IsDirty)
            {
                IReadOnlyList<Animation> gltfAnimations = gltfContainerComponent.Promise.Result!.Value.Asset!.Animations;
                pbAnimator.IsDirty = false;

                foreach (var animation in gltfAnimations)
                    animation!.SetAnimationState(pbAnimator.States!);
            }
        }

        [Query]
        [All(typeof(AnimationLoadedComponent))]
        [None(typeof(PBAnimator), typeof(DeleteEntityIntention))]
        private void HandleComponentRemoval(ref GltfContainerComponent gltfContainerComponent)
        {
            //If the Animator is removed, the animation should behave as if there was no animator, so play automatically and in a loop

            List<Animation> gltfAnimations = gltfContainerComponent.Promise.Result!.Value.Asset!.Animations;

            foreach (Animation animation in gltfAnimations)
                animation.Initialize();
        }
    }
}
