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
            LoadAnimatorQuery(World!);
            UpdateAnimationStateQuery(World!);
            HandleComponentRemovalQuery(World!);
            World!.Remove<SDKAnimatorComponent>(in HandleComponentRemoval_QueryDescription);
        }

        [Query]
        [None(typeof(SDKAnimatorComponent), typeof(LegacyGltfAnimation))]
        private void LoadAnimator(in Entity entity, ref PBAnimator pbAnimator, ref GltfContainerComponent gltfContainerComponent)
        {
            // Until the GLTF Container is not fully loaded (and it has at least one animation) we do not create the SDKAnimator
            if (gltfContainerComponent.IsSuccessfullyFinished() == false) return;
            if (gltfContainerComponent.Promise.Result.Value.Asset.Animators.Count == 0) return;

            foreach (UAnimator animator in gltfContainerComponent.Promise.Result.Value.Asset.Animators)
                InitializeAnimator(animator);

            var sdkAnimatorComponent = SDKAnimatorComponent.NewComponentFromPbAnimator(pbAnimator);

            World.Add(entity, sdkAnimatorComponent);

            // The PBAnimator is only dirtied on SDK side either on Create/CreateOrReplace
            // or when doing changes to it when triggered by events on the scene, so we never set it to true on the client.
            pbAnimator.IsDirty = false;
        }

        [Query]
        [None(typeof(LegacyGltfAnimation))]
        private void UpdateAnimationState(ref SDKAnimatorComponent sdkAnimatorComponent, ref GltfContainerComponent gltfContainerComponent)
        {
            if (gltfContainerComponent.State != LoadingState.Finished) return;

            if (sdkAnimatorComponent.TryConsumeAndUnDirt(out var state))
            {
                IEnumerable<UAnimator> animators = gltfContainerComponent.Promise.Result!.Value.Asset.Animators;

                foreach (var animator in animators)
                    SetAnimationState(state, animator);
            }
        }

        [Query]
        [None(typeof(PBAnimator), typeof(DeleteEntityIntention), typeof(LegacyGltfAnimation))]
        private void HandleComponentRemoval(ref GltfContainerComponent gltfContainerComponent, ref SDKAnimatorComponent sdkAnimatorComponent)
        {
            IEnumerable<UAnimator> gltfAnimations = gltfContainerComponent.Promise.Result!.Value.Asset.Animators;

            foreach (UAnimator animator in gltfAnimations)
                InitializeAnimator(animator);

            sdkAnimatorComponent.Dispose();
        }

        private static void InitializeAnimator(UAnimator animator)
        {
            animator.enabled = true;
        }

        private void SetAnimationState(IReadOnlyCollection<SDKAnimationState> sdkAnimationStates, UAnimator animator)
        {
            if (sdkAnimationStates.Count == 0)
                return;

            bool isAnyAnimPlaying = sdkAnimationStates.IsAnyPlaying();

            if (isAnyAnimPlaying)
                animator.enabled = true;

            foreach (SDKAnimationState sdkAnimationState in sdkAnimationStates)
            {
                string name = sdkAnimationState.Clip;
                int layerIndex = animator.GetLayerIndex(name);

                if (layerIndex == -1)
                {
                    ReportHub.LogWarning(new ReportData(GetReportCategory()), $"Cannot find animator layer for clip {name}");
                    continue;
                }

                if (sdkAnimationState.ShouldReset)
                {
                    animator.SetBool($"{name}_Enabled", false);
                    animator.SetBool($"{name}_Loop", false);
                    animator.ResetTrigger($"{name}_Trigger");
                    animator.SetLayerWeight(layerIndex, 0f);
                    continue;
                }

                animator.SetBool($"{name}_Enabled", sdkAnimationState.Playing);
                animator.SetBool($"{name}_Loop", sdkAnimationState.Loop);

                // TODO: it could be an edge case due sdkAnimationState.ShouldReset.. support it if need it

                if (sdkAnimationState.Playing)
                {
                    animator.SetLayerWeight(layerIndex, sdkAnimationState.Weight);

                    bool isAnimationAlreadyPlaying = animator.GetCurrentAnimatorStateInfo(layerIndex).IsName(name);

                    // This check fixes a border case that happens on sdk-goerli-plaza pirate island (78,7) with opening the chest the first time.
                    // The AB converter sets the first clip in the animator to play as default, since it is required by the sdk definitions.
                    // The scene first asks to play another clip (not the default one).
                    // Eventually the scene finally decides to play the default clip, but it was already playing since it was never modified.
                    // So the trigger gets enabled and makes the execution play twice (after it finishes), although is set to loop:false
                    // The fix consists on avoid triggering the animation if it is already playing, to avoid stacking the trigger.
                    if (!isAnimationAlreadyPlaying)
                        animator.SetTrigger($"{name}_Trigger");

                    // Animators don't support speed by state, just a global speed
                    animator.speed = sdkAnimationState.Speed;

                    // The animation state is reset automatically when the state is changed, either stops playing or exit on loop:false,
                    // it is how the animator works
                    // This behaviour could bring unexpected results since it works differently than unity-renderer
                }
                else
                {
                    // Since animation states are now executed through layers, we need to force to 0 if the state is not playing
                    // otherwise it overrides the current playing state
                    animator.SetLayerWeight(layerIndex, 0f);
                }
            }

            if (!isAnyAnimPlaying && animator.enabled)
            {
                // We need to apply the latest state before disabling the animator, otherwise we might get artifacts
                // Like the turrets in metadynelabs.dcl.eth whose stops in the middle of the explosion animation when resetting the level
                animator.Update(0);
                animator.enabled = false;
            }
        }
    }
}
