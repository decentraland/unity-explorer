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
using ECS.Unity.Groups;
using ECS.Unity.Transforms.Components;
using System.Collections.Generic;
using CrdtEcsBridge.Components.Transform;
using ECS.Groups;
using ECS.Unity.Transforms.Systems;
using UnityEngine;
using static DCL.ECSComponents.EasingFunction;
using static DG.Tweening.Ease;

namespace DCL.SDKComponents.Tween.Systems
{
    [UpdateInGroup(typeof(SyncedSimulationSystemGroup))]
    [UpdateBefore(typeof(UpdateTransformSystem))]
    /*[UpdateInGroup(typeof(ComponentInstantiationGroup))]
    [UpdateAfter(typeof(TweenLoaderSystem))]*/
    [LogCategory(ReportCategory.TWEEN)]
    [ThrottlingEnabled]
    public partial class TweenUpdaterSystem : BaseUnityLoopSystem, IFinalizeWorldSystem
    {
        private const int MILLISECONDS_CONVERSION_INT = 1000;

        private static readonly Dictionary<EasingFunction, Ease> EASING_FUNCTIONS_MAP = new ()
        {
            [EfLinear] = Linear,
            [EfEaseinsine] = InSine,
            [EfEaseoutsine] = OutSine,
            [EfEasesine] = InOutSine,
            [EfEaseinquad] = InQuad,
            [EfEaseoutquad] = OutQuad,
            [EfEasequad] = InOutQuad,
            [EfEaseinexpo] = InExpo,
            [EfEaseoutexpo] = OutExpo,
            [EfEaseexpo] = InOutExpo,
            [EfEaseinelastic] = InElastic,
            [EfEaseoutelastic] = OutElastic,
            [EfEaseelastic] = InOutElastic,
            [EfEaseinbounce] = InBounce,
            [EfEaseoutbounce] = OutBounce,
            [EfEasebounce] = InOutBounce,
            [EfEaseincubic] = InCubic,
            [EfEaseoutcubic] = OutCubic,
            [EfEasecubic] = InOutCubic,
            [EfEaseinquart] = InQuart,
            [EfEaseoutquart] = OutQuart,
            [EfEasequart] = InOutQuart,
            [EfEaseinquint] = InQuint,
            [EfEaseoutquint] = OutQuint,
            [EfEasequint] = InOutQuint,
            [EfEaseincirc] = InCirc,
            [EfEaseoutcirc] = OutCirc,
            [EfEasecirc] = InOutCirc,
            [EfEaseinback] = InBack,
            [EfEaseoutback] = OutBack,
            [EfEaseback] = InOutBack,
        };

        private readonly IECSToCRDTWriter ecsToCRDTWriter;
        private readonly List<DG.Tweening.Tween> transformTweens = new ();
        private Tweener tempTweener;

        public TweenUpdaterSystem(World world, IECSToCRDTWriter ecsToCRDTWriter) : base(world)
        {
            this.ecsToCRDTWriter = ecsToCRDTWriter;
        }

        protected override void Update(float t)
        {
            UpdateTweenSequenceQuery(World);

            HandleEntityDestructionQuery(World);
            HandleComponentRemovalQuery(World);

            World.Remove<SDKTweenComponent>(in HandleEntityDestruction_QueryDescription);
            World.Remove<SDKTweenComponent>(in HandleComponentRemoval_QueryDescription);
        }

        public void FinalizeComponents(in Query query)
        {
            FinalizeComponentsQuery(World);
        }

        [Query]
        [All(typeof(SDKTweenComponent))]
        private void FinalizeComponents(ref CRDTEntity sdkEntity, ref SDKTweenComponent tweenComponent)
        {
            CleanUpTweenBeforeRemoval(sdkEntity, tweenComponent);
        }

        [Query]
        [All(typeof(DeleteEntityIntention))]
        private void HandleEntityDestruction(ref SDKTweenComponent tweenComponent, ref CRDTEntity sdkEntity)
        {
            CleanUpTweenBeforeRemoval(sdkEntity, tweenComponent);
        }

        [Query]
        [None(typeof(PBTween), typeof(DeleteEntityIntention))]
        private void HandleComponentRemoval(ref SDKTweenComponent tweenComponent, ref CRDTEntity sdkEntity)
        {
            CleanUpTweenBeforeRemoval(sdkEntity, tweenComponent);
        }

        [Query]
        [All(typeof(SDKTweenComponent))]
        private void UpdateTweenSequence(ref PBTween pbTween, ref SDKTweenComponent sdkTweenComponent, ref TransformComponent transformComponent, ref CRDTEntity sdkEntity, ref SDKTransform sdkTransform)
        {
            if (!sdkTweenComponent.IsDirty)
            {
                UpdateTweenState(sdkEntity, ref sdkTweenComponent, ref sdkTransform);
            }
            else
            {
                bool isPlaying = !pbTween.HasPlaying || pbTween.Playing;
                sdkTweenComponent.IsPlaying = isPlaying;

                Transform entityTransform = transformComponent.Transform;
                float durationInSeconds = pbTween.Duration / MILLISECONDS_CONVERSION_INT;

                SetupTweener(transformComponent, ref sdkTweenComponent, entityTransform, pbTween, durationInSeconds, isPlaying);

                sdkTweenComponent.Tweener = tempTweener;
                sdkTweenComponent.CurrentTime = pbTween.CurrentTime;
                sdkTweenComponent.IsDirty = false;

                if (isPlaying)
                {
                    sdkTweenComponent.Tweener.Play();
                    sdkTweenComponent.TweenStateStatus = sdkTweenComponent.CurrentTime.Equals(1f) ? TweenStateStatus.TsCompleted : TweenStateStatus.TsActive;
                }
                else
                {
                    sdkTweenComponent.Tweener.Pause();
                    sdkTweenComponent.TweenStateStatus = TweenStateStatus.TsPaused;
                }
            }
        }

        private void UpdateTweenState(CRDTEntity sdkEntity, ref SDKTweenComponent sdkTweenComponent, ref SDKTransform sdkTransform)
        {
            //float currentTime = sdkTweenComponent.Tweener.ElapsedPercentage();
            float currentTime = 0;
            if (sdkTweenComponent.Vector3Tweener != null)
            {
                currentTime = sdkTweenComponent.Vector3Tweener.ElapsedPercentage();
            }
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

            //Debug.Log($"JUANI 2 {sdkTweenComponent.HelperTransform.localRotation.eulerAngles}");


            if (sdkTweenComponent.Vector3Tweener != null)
            {
                sdkTweenComponent.HelperSDKTransform.Position = sdkTweenComponent.Vector3Tweener.currentValue;
                Debug.Log($"JUANI {sdkTweenComponent.Vector3Tweener.currentValue}");
            }

            //sdkTweenComponent.HelperSDKTransform.Rotation = sdkTweenComponent.HelperTransform.localRotation;
            //sdkTweenComponent.HelperSDKTransform.Scale = sdkTweenComponent.HelperTransform.localScale;

            TweenSDKComponentHelper.WriteTweenState(ecsToCRDTWriter, sdkEntity, sdkTweenComponent.TweenStateStatus);
            TweenSDKComponentHelper.WriteTweenTransform(ecsToCRDTWriter, sdkEntity, sdkTweenComponent.HelperSDKTransform);

            sdkTransform.IsDirty = true;
            if (sdkTweenComponent.Vector3Tweener != null)
                sdkTransform.Position = sdkTweenComponent.Vector3Tweener.currentValue;

            //sdkTransform.Rotation = sdkTweenComponent.HelperTransform.localRotation;
            //sdkTransform.Scale = sdkTweenComponent.HelperTransform.localScale;
        }

        private void SetupTweener(TransformComponent transformComponent, ref SDKTweenComponent sdkTweenComponent, Transform entityTransform, PBTween tweenModel, float durationInSeconds, bool isPlaying)
        {
            tempTweener = sdkTweenComponent.Tweener;

            //NOTE: Left this per legacy reasons, Im not sure if this can happen in new renderer
            // There may be a tween running for the entity transform, e.g: during preview mode hot-reload.
            //TODO: Juani (this is not longer supported)
            DOTween.TweensByTarget(entityTransform, true, transformTweens);
            if (transformTweens.Count > 0) transformTweens[0].Rewind(false);

            if (!EASING_FUNCTIONS_MAP.TryGetValue(tweenModel.EasingFunction, out Ease ease))
                ease = Linear;

            switch (tweenModel.ModeCase)
            {
                case PBTween.ModeOneofCase.Rotate:
                    tempTweener = SetupRotationTween(transformComponent.Transform,
                        PrimitivesConversionExtensions.PBQuaternionToUnityQuaternion(tweenModel.Rotate.Start),
                        PrimitivesConversionExtensions.PBQuaternionToUnityQuaternion(tweenModel.Rotate.End),
                        durationInSeconds, ease,
                        ref sdkTweenComponent);

                    break;
                case PBTween.ModeOneofCase.Scale:
                    tempTweener = SetupScaleTween(transformComponent.Transform,
                        PrimitivesConversionExtensions.PBVectorToUnityVector(tweenModel.Scale.Start),
                        PrimitivesConversionExtensions.PBVectorToUnityVector(tweenModel.Scale.End),
                        durationInSeconds, ease);

                    break;
                case PBTween.ModeOneofCase.Move:
                default:
                    SetupPositionTween(transformComponent.Transform,
                        PrimitivesConversionExtensions.PBVectorToUnityVector(tweenModel.Move.Start),
                        PrimitivesConversionExtensions.PBVectorToUnityVector(tweenModel.Move.End),
                        durationInSeconds, ease, tweenModel.Move.HasFaceDirection && tweenModel.Move.FaceDirection,
                        ref sdkTweenComponent);

                    break;
            }

            tempTweener.Goto(tweenModel.CurrentTime * durationInSeconds, isPlaying);
        }

        private void CleanUpTweenBeforeRemoval(CRDTEntity sdkEntity, SDKTweenComponent sdkTweenComponent)
        {
            sdkTweenComponent.Tweener.Kill();
            ecsToCRDTWriter.DeleteMessage<PBTweenState>(sdkEntity);
        }

        private void SetupPositionTween(Transform entityTransform, Vector3 startPosition,
            Vector3 endPosition, float durationInSeconds, Ease ease, bool faceDirection,
            ref SDKTweenComponent tweenComponent)
        {
            if (faceDirection) entityTransform.forward = (endPosition - startPosition).normalized;

            entityTransform.localPosition = startPosition;

            tweenComponent.Vector3Tweener = new Vector3Tweener();
            tweenComponent.Vector3Tweener.TransformVector3(startPosition, endPosition, durationInSeconds, ease);
            //return tweenComponent.HelperTransform.DOLocalMove(endPosition, durationInSeconds).SetEase(ease).SetAutoKill(false);
        }

        private TweenStateStatus GetCurrentTweenState(float currentTime, bool isPlaying)
        {
            if (!isPlaying) { return TweenStateStatus.TsPaused; }

            return currentTime.Equals(1f) ? TweenStateStatus.TsCompleted : TweenStateStatus.TsActive;
        }

        private Tweener SetupRotationTween(Transform entityTransform, Quaternion startRotation,
            Quaternion endRotation, float durationInSeconds, Ease ease, ref SDKTweenComponent tweenComponent)
        {
            entityTransform.localRotation = startRotation;
            //tweenComponent.HelperTransform.DOLocalRotateQuaternion(endRotation, durationInSeconds).SetEase(ease).SetAutoKill(false);
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
