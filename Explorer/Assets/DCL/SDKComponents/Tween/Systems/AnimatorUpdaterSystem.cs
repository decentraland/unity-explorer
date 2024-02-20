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
        private const int MILLISECONDS_CONVERSION_INT = 1000;

        private readonly IECSToCRDTWriter ecsToCRDTWriter;
        private readonly IComponentPool<SDKAnimatorComponent> sdkAnimatorPool;
        private readonly IComponentPool<SDKAnimationState> sdkAnimationStatePool;
        private readonly List<DG.Tweening.Tween> transformTweens = new ();
        private Tweener tempTweener;

        public AnimatorUpdaterSystem(World world, IECSToCRDTWriter ecsToCRDTWriter, IComponentPool<SDKAnimatorComponent> sdkAnimatorPool, IComponentPool<SDKAnimationState> sdkAnimationStatePool) : base(world)
        {
            this.ecsToCRDTWriter = ecsToCRDTWriter;
            this.sdkAnimatorPool = sdkAnimatorPool;
            this.sdkAnimationStatePool = sdkAnimationStatePool;
        }

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
        [All(typeof(TweenComponent))]
        private void FinalizeComponents(ref CRDTEntity sdkEntity, ref TweenComponent tweenComponent)
        {
            CleanUpTweenBeforeRemoval(sdkEntity, tweenComponent.SDKTweenComponent);
        }

        [Query]
        [All(typeof(DeleteEntityIntention))]
        private void HandleEntityDestruction(ref TweenComponent tweenComponent, ref CRDTEntity sdkEntity)
        {
            CleanUpTweenBeforeRemoval(sdkEntity, tweenComponent.SDKTweenComponent);
        }

        [Query]
        [None(typeof(PBTween), typeof(DeleteEntityIntention))]
        private void HandleComponentRemoval(ref TweenComponent tweenComponent, ref CRDTEntity sdkEntity)
        {
            CleanUpTweenBeforeRemoval(sdkEntity, tweenComponent.SDKTweenComponent);
        }

        [Query]
        [All(typeof(AnimatorComponent))]
        private void UpdateAnimationState(ref AnimatorComponent animatorComponent, ref GltfContainerComponent component)
        {
            if (component.State.Value != LoadingState.Finished || !animatorComponent.SDKAnimatorComponent.IsDirty) return;

            if (component.Promise is not { Result: { } }) return;

            if (component.Promise.Result.Value.Asset.Animations.Count == 0) return;

            {
                animatorComponent.SDKAnimatorComponent.IsDirty = false;

                if (!animatorComponent.SDKAnimatorComponent.SDKAnimation.IsInitialized)
                {
                    SetupAnimation(component.Promise.Result.Value.Asset.Animations.First());
                    animatorComponent.SDKAnimatorComponent.SDKAnimation.IsInitialized = true;
                }

                SetAnimationState(animatorComponent.SDKAnimatorComponent.SDKAnimationStates, component.Promise.Result.Value.Asset.Animations.First());
            }
        }

        private static void SetupAnimation(Animation animation)
        {
            int layerIndex = 0;

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


        private static void SetAnimationState(IList<SDKAnimationState> animationState, Animation animation)
        {
            if (animationState.Count == 0)
                return;

            for (int i = 0; i < animationState.Count; i++)
            {
                var state = animationState[i];
                AnimationState unityState = animation[state.Clip];

                if (!unityState)
                    continue;

                unityState.weight = state.Weight;

                unityState.wrapMode = state.Loop ? WrapMode.Loop : WrapMode.Default;

                unityState.clip.wrapMode = unityState.wrapMode;
                unityState.speed = state.Speed;
                unityState.enabled = state.Playing;

                if (state.ShouldReset && animation.IsPlaying(state.Clip))
                {
                    animation.Stop(state.Clip);

                    //Manually sample the animation. If the reset is not played again the frame 0 wont be applied
                    unityState.clip.SampleAnimation(animation.gameObject, 0);
                }

                if (state.Playing && !animation.IsPlaying(state.Clip))
                    animation.Play(state.Clip);
            }
        }


        private void UpdateTweenState(TransformComponent transformComponent, CRDTEntity sdkEntity, SDKTweenComponent sdkTweenComponent)
        {
            float currentTime = sdkTweenComponent.Tweener.ElapsedPercentage();
            var tweenStateDirty = false;
            TweenStateStatus newState = GetCurrentTweenState(currentTime, sdkTweenComponent.IsPlaying);

            //We only update the state if we changed status OR if the tween is playing and the current time has changed
            if (newState != sdkTweenComponent.TweenStateStatus)
            {
                sdkTweenComponent.TweenStateStatus = newState;
                tweenStateDirty = true;
            }

            if (sdkTweenComponent.IsPlaying && !sdkTweenComponent.CurrentTime.Equals(currentTime))
            {
                sdkTweenComponent.CurrentTime = currentTime;
                tweenStateDirty = true;
            }

            if (!tweenStateDirty) return;

            TweenSDKComponentHelper.WriteTweenState(ecsToCRDTWriter, sdkEntity, sdkTweenComponent.TweenStateStatus);
            TweenSDKComponentHelper.WriteTweenTransform(ecsToCRDTWriter, sdkEntity, transformComponent);
        }

        private void SetupTweener(TransformComponent transformComponent, SDKTweenComponent sdkTweenComponent, Transform entityTransform, SDKTweenModel tweenModel, float durationInSeconds, bool isPlaying)
        {
            tempTweener = sdkTweenComponent.Tweener;

            //NOTE: Left this per legacy reasons, Im not sure if this can happen in new renderer
            // There may be a tween running for the entity transform, e.g: during preview mode hot-reload.
            DOTween.TweensByTarget(entityTransform, true, transformTweens);
            if (transformTweens.Count > 0) transformTweens[0].Rewind(false);

            tempTweener.Goto(tweenModel.CurrentTime * durationInSeconds, isPlaying);
        }

        private void CleanUpTweenBeforeRemoval(CRDTEntity sdkEntity, SDKTweenComponent sdkTweenComponent)
        {
            //sdkTweenComponent.Tweener.Kill();
            //ecsToCRDTWriter.DeleteMessage<PBTweenState>(sdkEntity);
            //tweenComponentPool.Release(sdkTweenComponent);
        }

        private Tweener SetupPositionTween(Transform entityTransform, Vector3 startPosition,
            Vector3 endPosition, float durationInSeconds, Ease ease, bool faceDirection)
        {
            if (faceDirection) entityTransform.forward = (endPosition - startPosition).normalized;

            entityTransform.localPosition = startPosition;
            return entityTransform.DOLocalMove(endPosition, durationInSeconds).SetEase(ease).SetAutoKill(false);
        }

        private TweenStateStatus GetCurrentTweenState(float currentTime, bool isPlaying)
        {
            if (!isPlaying) { return TweenStateStatus.TsPaused; }

            return currentTime.Equals(1f) ? TweenStateStatus.TsCompleted : TweenStateStatus.TsActive;
        }

        private Tweener SetupRotationTween(Transform entityTransform, Quaternion startRotation,
            Quaternion endRotation, float durationInSeconds, Ease ease)
        {
            entityTransform.localRotation = startRotation;
            return entityTransform.DOLocalRotateQuaternion(endRotation, durationInSeconds).SetEase(ease).SetAutoKill(false);
        }

        private Tweener SetupScaleTween(Transform entityTransform, Vector3 startScale,
            Vector3 endScale, float durationInSeconds, Ease ease)
        {
            entityTransform.localScale = startScale;
            return entityTransform.DOScale(endScale, durationInSeconds).SetEase(ease).SetAutoKill(false);
        }
    }
}
