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
            World!.Remove<LoadedAnimationsComponent>(in HandleComponentRemoval_QueryDescription);
        }

        [Query]
        [None(typeof(LoadedAnimationsComponent))]
        private void LoadAnimator(in Entity entity, ref GltfContainerComponent gltfContainerComponent)
        {
            if (GltfReady(gltfContainerComponent) == false)
                return;

            World!.Add(entity, new LoadedAnimationsComponent(gltfContainerComponent.Promise.Result!.Value.Asset!.Animations));
        }

        //Until the GLTF Container is not fully loaded (and it has at least one animation) we do not create the SDKAnimator
        [Query]
        private void UpdateAnimationState(in Entity entity, ref PBAnimator pbAnimator, ref LoadedAnimationsComponent loadedAnimations)
        {
            //if PBAnimator is dirty, we need to update the SDKAnimatorComponent

            if (pbAnimator.IsDirty)
            {
                var tuple = LoadedAnimation.RequiredAnimations(pbAnimator.States!);

                foreach (var animation in loadedAnimations.List)
                    animation.Apply(tuple.playingAnimation, tuple.stoppedAnimation);

                pbAnimator.IsDirty = false;
            }

            foreach (LoadedAnimation loadedAnimation in loadedAnimations.List)
                loadedAnimation.Update();
        }

        [Query]
        [None(typeof(PBAnimator), typeof(DeleteEntityIntention))]
        private void HandleComponentRemoval(ref LoadedAnimationsComponent loadedAnimations)
        {
            //If the Animator is removed, the animation should behave as if there was no animator, so play automatically and in a loop
            foreach (var animation in loadedAnimations.List)
                animation.Initialize();
        }

        private static bool GltfReady(GltfContainerComponent gltfContainerComponent)
        {
            if (gltfContainerComponent.State.Value != LoadingState.Finished) return false;
            if (gltfContainerComponent.Promise is not { Result: { } }) return false;
            if (gltfContainerComponent.Promise.Result.Value.Asset?.Animations.Count == 0) return false;

            return true;
        }
    }
}
