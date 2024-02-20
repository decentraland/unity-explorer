using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using CRDT;
using CrdtEcsBridge.Components.Conversion;
using CrdtEcsBridge.ECSToCRDTWriter;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Optimization.Pools;
using DCL.SDKComponents.Tween.Components;
using DCL.SDKComponents.Tween.Helpers;
using DG.Tweening;
using ECS.Abstract;
using ECS.LifeCycle;
using ECS.LifeCycle.Components;
using ECS.Unity.GLTFContainer.Components;
using ECS.Unity.Groups;
using ECS.Unity.Transforms.Components;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static DCL.ECSComponents.EasingFunction;
using static DG.Tweening.Ease;

namespace DCL.SDKComponents.Tween.Systems
{
    [UpdateInGroup(typeof(ComponentInstantiationGroup))]
    [UpdateAfter(typeof(TweenLoaderSystem))]
    [LogCategory(ReportCategory.TWEEN)]
    [ThrottlingEnabled]
    public partial class AnimatorUpdaterSystem : BaseUnityLoopSystem, IFinalizeWorldSystem
    {
        public AnimatorUpdaterSystem(World world) : base(world) { }

        protected override void Update(float t)
        {
            UpdateAnimationStateQuery(World);

            //HandleEntityDestructionQuery(World);
            //HandleComponentRemovalQuery(World);

            //World.Remove<TweenComponent>(in HandleEntityDestruction_QueryDescription);
            //World.Remove<TweenComponent>(in HandleComponentRemoval_QueryDescription);
        }

        public void FinalizeComponents(in Query query)
        {
            FinalizeComponentsQuery(World);
        }

        [Query]
        [All(typeof(SDKAnimatorComponent))]
        private void FinalizeComponents(ref CRDTEntity sdkEntity, ref SDKAnimatorComponent tweenComponent)
        {
        }

        [Query]
        [All(typeof(DeleteEntityIntention))]
        private void HandleEntityDestruction(ref SDKAnimatorComponent sdkAnimatorComponent)
        {
        }

        [Query]
        [None(typeof(PBTween), typeof(DeleteEntityIntention))]
        private void HandleComponentRemoval(ref SDKAnimatorComponent sdkAnimatorComponent)
        {
        }

        [Query]
        [All(typeof(SDKAnimatorComponent))]
        private void UpdateAnimationState(ref SDKAnimatorComponent animatorComponent, ref GltfContainerComponent gltfContainerComponent)
        {
            if (gltfContainerComponent.State.Value != LoadingState.Finished || !animatorComponent.IsDirty) return;

            if (gltfContainerComponent.Promise is not { Result: { } }) return;

            List<Animation> gltfAnimations = gltfContainerComponent.Promise.Result.Value.Asset.Animations;
            if (gltfAnimations.Count == 0) return;

            {
                animatorComponent.IsDirty = false;

                if (!animatorComponent.SDKAnimation.IsInitialized)
                {
                    foreach (Animation animation in gltfAnimations) { SetupAnimation(animation); }

                    animatorComponent.SDKAnimation.IsInitialized = true;
                }

                foreach (Animation animation in gltfAnimations) { SetAnimationState(animatorComponent.SDKAnimationStates, animation); }
            }
        }

        private static void SetupAnimation(Animation animation)
        {
            var layerIndex = 0;

            animation.playAutomatically = true;
            animation.enabled = true;
            animation.Stop();

            //putting the component in play state if playAutomatically was true at that point.
            if (animation.clip)
                animation.clip.SampleAnimation(animation.gameObject, 0);

            foreach (AnimationState unityState in animation)
            {
                unityState.clip.wrapMode = WrapMode.Loop;
                unityState.layer = layerIndex;
                unityState.blendMode = AnimationBlendMode.Blend;
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
