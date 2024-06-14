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
using System.Collections.Generic;
using UnityEngine.Pool;
using UAnimator = UnityEngine.Animator;

namespace DCL.SDKComponents.Animator.Systems
{
    [UpdateInGroup(typeof(ComponentInstantiationGroup))]
    [LogCategory(ReportCategory.ANIMATOR)]
    [ThrottlingEnabled]
    public partial class AnimationPlayerSystem : BaseUnityLoopSystem
    {
        private static readonly int LOOP_PARAM = UAnimator.StringToHash("Loop");

        public AnimationPlayerSystem(World world) : base(world) { }

        protected override void Update(float t)
        {
            LoadAnimatorQuery(World);
            UpdateAnimationStateQuery(World, t);
            HandleComponentRemovalQuery(World);
            World.Remove<SDKAnimatorComponent>(in HandleComponentRemoval_QueryDescription);
        }

        [Query]
        [None(typeof(SDKAnimatorComponent))]
        private void LoadAnimator(in Entity entity, ref PBAnimator pbAnimator, ref GltfContainerComponent gltfContainerComponent)
        {
            // Until the GLTF Container is not fully loaded (and it has at least one animation) we do not create the SDKAnimator
            if (gltfContainerComponent.State != LoadingState.Finished) return;
            if (gltfContainerComponent.Promise.Result?.Asset == null) return;
            if (gltfContainerComponent.Promise.Result.Value.Asset.Animators.Count == 0) return;

            foreach (UAnimator animator in gltfContainerComponent.Promise.Result.Value.Asset.Animators)
                InitializeAnimator(animator);

            List<SDKAnimationState> sdkAnimationStates = ListPool<SDKAnimationState>.Get();

            for (var i = 0; i < pbAnimator.States.Count; i++)
            {
                PBAnimationState pbAnimationState = pbAnimator.States[i];
                var sdkAnimationState = new SDKAnimationState(pbAnimationState);
                sdkAnimationStates.Add(sdkAnimationState);
            }

            var sdkAnimatorComponent = new SDKAnimatorComponent(sdkAnimationStates)
                {
                    IsDirty = true,
                };

            World.Add(entity, sdkAnimatorComponent);
            // The PBAnimator is only dirtied on SDK side either on Create/CreateOrReplace
            // or when doing changes to it when triggered by events on the scene, so we never set it to true on the client.
            pbAnimator.IsDirty = false;
        }

        [Query]
        private void UpdateAnimationState([Data] float dt, ref SDKAnimatorComponent sdkAnimatorComponent, ref GltfContainerComponent gltfContainerComponent)
        {
            if (!sdkAnimatorComponent.IsDirty) return;
            if (gltfContainerComponent.Promise.Result?.Asset == null) return;
            if (gltfContainerComponent.Promise.Result.Value.Asset.Animators.Count == 0) return;

            List<UAnimator> animators = gltfContainerComponent.Promise.Result.Value.Asset.Animators;
            sdkAnimatorComponent.IsDirty = false;

            foreach (var animator in animators)
                SetAnimationState(sdkAnimatorComponent.SDKAnimationStates, animator, dt);
        }

        [Query]
        [None(typeof(PBAnimator), typeof(DeleteEntityIntention))]
        private void HandleComponentRemoval(ref GltfContainerComponent gltfContainerComponent, ref SDKAnimatorComponent sdkAnimatorComponent)
        {
            //If the Animator is removed, the animation should behave as if there was no animator, so play automatically and in a loop
            if (gltfContainerComponent.Promise.Result?.Asset == null) return;
            if (gltfContainerComponent.Promise.Result.Value.Asset.Animators.Count == 0) return;

            List<UAnimator> gltfAnimations = gltfContainerComponent.Promise.Result.Value.Asset.Animators;

            foreach (UAnimator animator in gltfAnimations)
                InitializeAnimator(animator);

            ListPool<SDKAnimationState>.Release(sdkAnimatorComponent.SDKAnimationStates);
        }

        private static void InitializeAnimator(UAnimator animator)
        {
            animator.enabled = true;
        }

        private static void SetAnimationState(ICollection<SDKAnimationState> sdkAnimationStates, UAnimator animator, float dt)
        {
            const int DEFAULT_LAYER_INDEX = 0;

            if (sdkAnimationStates.Count == 0)
                return;

            var isAnyAnimPlaying = false;

            foreach (SDKAnimationState sdkAnimationState in sdkAnimationStates)
                isAnyAnimPlaying |= sdkAnimationState.Playing;

            if (!isAnyAnimPlaying)
            {
                // Reset to anim initial state
                animator.CrossFade(animator.GetCurrentAnimatorStateInfo(DEFAULT_LAYER_INDEX).fullPathHash, 0f);
                animator.Update(dt);
                animator.enabled = false;
                return;
            }

            animator.enabled = true;

            foreach (SDKAnimationState sdkAnimationState in sdkAnimationStates)
            {
                if (!sdkAnimationState.Playing) continue;

                animator.SetLayerWeight(DEFAULT_LAYER_INDEX, sdkAnimationState.Weight);
                animator.SetBool(LOOP_PARAM, sdkAnimationState.Loop);
                animator.speed = sdkAnimationState.Speed;
                animator.SetTrigger(sdkAnimationState.Clip);

                if (sdkAnimationState.ShouldReset)
                    animator.CrossFade(sdkAnimationState.Clip, 0f);
            }
        }
    }
}
