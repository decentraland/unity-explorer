using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.SDKComponents.Animator.Components;
using ECS.Abstract;
using ECS.LifeCycle.Components;
using ECS.Unity.GLTFContainer.Components;
using ECS.Unity.Groups;
using Google.Protobuf.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

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
            LoadAnimatorQuery(World);
            UpdateAnimatorQuery(World);

            UpdateAnimationStateQuery(World);

            HandleEntityDeletionQuery(World);
            HandleComponentRemovalQuery(World);

            World.Remove<SDKAnimatorComponent>(in HandleEntityDeletion_QueryDescription);
            World.Remove<SDKAnimatorComponent>(in HandleComponentRemoval_QueryDescription);
        }

        [Query]
        [All(typeof(DeleteEntityIntention))]
        private void HandleEntityDeletion(ref SDKAnimatorComponent sdkAnimatorComponent)
        {
            ListPool<SDKAnimationState>.Release(sdkAnimatorComponent.SDKAnimationStates);
        }

        [Query]
        [None(typeof(PBAnimator), typeof(DeleteEntityIntention))]
        private void HandleComponentRemoval(ref GltfContainerComponent gltfContainerComponent, ref SDKAnimatorComponent sdkAnimatorComponent)
        {
            //If the Animator is removed, the animation should behave as if there was no animator, so play automatically and in a loop

            List<Animation> gltfAnimations = gltfContainerComponent.Promise.Result.Value.Asset.Animations;

            foreach (Animation animation in gltfAnimations) { InitializeAnimation(animation); }

            ListPool<SDKAnimationState>.Release(sdkAnimatorComponent.SDKAnimationStates);
        }

        [Query]
        [None(typeof(SDKAnimatorComponent))]
        private void LoadAnimator(in Entity entity, ref PBAnimator pbAnimator, ref GltfContainerComponent gltfContainerComponent)
        {
            //Until the GLTF Container is not fully loaded (and it has at least one animation) we do not create the SDKAnimator
            if (gltfContainerComponent.State.Value != LoadingState.Finished) return;
            if (gltfContainerComponent.Promise is not { Result: { } }) return;
            if (gltfContainerComponent.Promise.Result.Value.Asset.Animations.Count == 0) return;

            foreach (Animation animation in gltfContainerComponent.Promise.Result.Value.Asset.Animations) { InitializeAnimation(animation); }

            List<SDKAnimationState> sdkAnimationStates = ListPool<SDKAnimationState>.Get();

            for (var i = 0; i < pbAnimator.States.Count; i++)
            {
                PBAnimationState pbAnimationState = pbAnimator.States[i];
                var sdkAnimationState = new SDKAnimationState(pbAnimationState);
                sdkAnimationStates.Add(sdkAnimationState);
            }

            var sdkAnimatorComponent = new SDKAnimatorComponent(sdkAnimationStates);

            World.Add(entity, sdkAnimatorComponent);
            pbAnimator.IsDirty = false;
        }

        [Query]
        private void UpdateAnimator(ref PBAnimator pbAnimator, ref SDKAnimatorComponent sdkAnimatorComponent)
        {
            if (!pbAnimator.IsDirty) return;

            sdkAnimatorComponent.IsDirty = true;
            pbAnimator.IsDirty = false;
            List<SDKAnimationState> sdkAnimationStates = sdkAnimatorComponent.SDKAnimationStates;
            sdkAnimationStates.Clear();

            RepeatedField<PBAnimationState> pbAnimatorStates = pbAnimator.States;

            for (var i = 0; i < pbAnimatorStates.Count; i++)
            {
                var sdkAnimationState = new SDKAnimationState(pbAnimatorStates[i]);
                sdkAnimationStates.Add(sdkAnimationState);
            }
        }

        [Query]
        private void UpdateAnimationState(ref SDKAnimatorComponent sdkAnimatorComponent, ref GltfContainerComponent gltfContainerComponent)
        {
            if (sdkAnimatorComponent.IsDirty)
            {
                List<Animation> gltfAnimations = gltfContainerComponent.Promise.Result.Value.Asset.Animations;
                sdkAnimatorComponent.IsDirty = false;

                foreach (Animation animation in gltfAnimations) { SetAnimationState(sdkAnimatorComponent.SDKAnimationStates, animation); }
            }
        }

        private static void InitializeAnimation(Animation animation)
        {
            var layerIndex = 0;

            animation.playAutomatically = true;
            animation.enabled = true;
            animation.Stop();

            //putting the component in play state if playAutomatically was true at that point.
            if (animation.clip)
                animation.clip.SampleAnimation(animation.gameObject, 0);

            foreach (AnimationState animationState in animation)
            {
                animationState.clip.wrapMode = WrapMode.Loop;
                animationState.layer = layerIndex;
                animationState.blendMode = AnimationBlendMode.Blend;
                layerIndex++;
            }
        }

        private static void SetAnimationState(IList<SDKAnimationState> sdkAnimationStates, Animation animation)
        {
            if (sdkAnimationStates.Count == 0)
                return;

            for (var i = 0; i < sdkAnimationStates.Count; i++)
            {
                SDKAnimationState state = sdkAnimationStates[i];
                AnimationState animationState = animation[state.Clip];

                if (!animationState) continue;

                animationState.weight = state.Weight;

                animationState.wrapMode = state.Loop ? WrapMode.Loop : WrapMode.Default;

                animationState.clip.wrapMode = animationState.wrapMode;
                animationState.speed = state.Speed;
                animationState.enabled = state.Playing;

                if (state.ShouldReset && animation.IsPlaying(state.Clip))
                {
                    animation.Stop(state.Clip);

                    //Manually sample the animation. If the reset is not played again the frame 0 wont be applied
                    animationState.clip.SampleAnimation(animation.gameObject, 0);
                }

                if (state.Playing && !animation.IsPlaying(state.Clip))
                    animation.Play(state.Clip);
            }
        }
    }
}
