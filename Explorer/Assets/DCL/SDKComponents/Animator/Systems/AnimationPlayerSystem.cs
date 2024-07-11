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
        public AnimationPlayerSystem(World world) : base(world) { }

        protected override void Update(float t)
        {
            LoadAnimatorQuery(World);
            UpdateAnimationStateQuery(World);
            HandleComponentRemovalQuery(World);
            World.Remove<SDKAnimatorComponent>(in HandleComponentRemoval_QueryDescription);
        }

        [Query]
        [None(typeof(SDKAnimatorComponent), typeof(LegacyGltfAnimation))]
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
        [None(typeof(LegacyGltfAnimation))]
        private void UpdateAnimationState(ref SDKAnimatorComponent sdkAnimatorComponent, ref GltfContainerComponent gltfContainerComponent)
        {
            if (!sdkAnimatorComponent.IsDirty) return;
            if (gltfContainerComponent.State != LoadingState.Finished) return;

            List<UAnimator> animators = gltfContainerComponent.Promise.Result!.Value.Asset.Animators;
            sdkAnimatorComponent.IsDirty = false;

            foreach (var animator in animators)
                SetAnimationState(sdkAnimatorComponent.SDKAnimationStates, animator);
        }

        [Query]
        [None(typeof(PBAnimator), typeof(DeleteEntityIntention), typeof(LegacyGltfAnimation))]
        private void HandleComponentRemoval(ref GltfContainerComponent gltfContainerComponent, ref SDKAnimatorComponent sdkAnimatorComponent)
        {
            List<UAnimator> gltfAnimations = gltfContainerComponent.Promise.Result!.Value.Asset.Animators;

            foreach (UAnimator animator in gltfAnimations)
                InitializeAnimator(animator);

            ListPool<SDKAnimationState>.Release(sdkAnimatorComponent.SDKAnimationStates);
        }

        private static void InitializeAnimator(UAnimator animator)
        {
            animator.enabled = true;
        }

        private void SetAnimationState(ICollection<SDKAnimationState> sdkAnimationStates, UAnimator animator)
        {
            if (sdkAnimationStates.Count == 0)
                return;

            var isAnyAnimPlaying = false;

            foreach (SDKAnimationState sdkAnimationState in sdkAnimationStates)
                isAnyAnimPlaying |= sdkAnimationState.Playing;

            animator.enabled = isAnyAnimPlaying;

            if (!isAnyAnimPlaying) return;

            foreach (SDKAnimationState sdkAnimationState in sdkAnimationStates)
            {
                string name = sdkAnimationState.Clip;
                int layerIndex = animator.GetLayerIndex(name);

                if (layerIndex == -1)
                {
                    ReportHub.LogWarning(new ReportData(GetReportCategory()), $"Cannot find animator layer for clip {name}");
                    continue;
                }

                animator.SetBool($"{name}_Enabled", sdkAnimationState.Playing);
                animator.SetBool($"{name}_Loop", sdkAnimationState.Loop);

                if (sdkAnimationState.Playing)
                {
                    // Set the weight to 1 to ensure the animation is visible, as some scenes may set the state to playing:true but assign a weight of 0.
                    // ie: Teleperformance (-93,109)
                    // Is this expected by the SDK? Should the scene fix the parameters?
                    animator.SetLayerWeight(layerIndex, sdkAnimationState.Weight > 0f
                        ? sdkAnimationState.Weight
                        : 1f);

                    animator.SetTrigger($"{name}_Trigger");

                    // Animators don't support speed by state, just a global speed
                    animator.speed = sdkAnimationState.Speed;

                    // The animation state is reset automatically when the state is changed, either stops playing or exit on loop:false,
                    // it is how the animator works
                    // This behaviour could bring unexpected results since it works differently than unity-renderer
                    // TODO: support reset
                    // sdkAnimationState.ShouldReset
                }
                else
                {
                    // Since animation states are now executed through layers, we need to force to 0 if the state is not playing
                    // otherwise it overrides the current playing state
                    animator.SetLayerWeight(layerIndex, 0f);
                }
            }
        }
    }
}
