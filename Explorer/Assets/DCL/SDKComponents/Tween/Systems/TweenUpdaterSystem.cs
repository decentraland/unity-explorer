using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using CrdtEcsBridge.Components.Transform;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Utilities;
using DG.Tweening;
using ECS.Abstract;
using ECS.LifeCycle;
using ECS.Unity.Groups;
using ECS.Unity.Transforms.Components;
using ECS.Unity.Tween.Components;
using System.Collections.Generic;
using UnityEngine;
using static DCL.ECSComponents.EasingFunction;
using static DG.Tweening.Ease;

namespace ECS.Unity.Tween.Systems
{
    [UpdateInGroup(typeof(ComponentInstantiationGroup))]
    [UpdateAfter(typeof(TweenLoaderSystem))]
    [LogCategory(ReportCategory.TWEEN)]
    [ThrottlingEnabled]
    public partial class TweenUpdaterSystem : BaseUnityLoopSystem, IFinalizeWorldSystem
    {
        private const int MILLISECONDS_CONVERSION_INT = 1000;

        private Tweener tempTweener;

        public TweenUpdaterSystem(World world) : base(world)
        {
        }

        protected override void Update(float t)
        {
            SetupTweenSequenceQuery(World);
        }

        public void FinalizeComponents(in Query query)
        {
            FinalizeComponentsQuery(World);
        }

        [Query]
        [All(typeof(SDKTweenComponent))]
        private void FinalizeComponents(in Entity entity, ref SDKTweenComponent sdkTweenComponent)
        {
            World.Remove<SDKTweenComponent>(entity);
        }

        private TweenStateStatus GetCurrentTweenState(float currentTime, bool isPlaying)
        {
            if (!isPlaying) { return TweenStateStatus.TsPaused; }

            return currentTime.Equals(1f) ? TweenStateStatus.TsCompleted : TweenStateStatus.TsActive;
        }

        [Query]
        [All(typeof(SDKTweenComponent))]
        private void SetupTweenSequence(in Entity entity, ref SDKTweenComponent sdkTweenComponent, ref TransformComponent transformComponent)
        {
            if (sdkTweenComponent.Removed)
            {
                sdkTweenComponent.Tweener.Kill();
                World.Remove<SDKTweenComponent>(entity);
                return;
            }

            if (!sdkTweenComponent.IsDirty)
            {
                float currentTime = sdkTweenComponent.Tweener.ElapsedPercentage();
                TweenStateStatus newState = GetCurrentTweenState(currentTime, sdkTweenComponent.isPlaying);

                //We only update the state if we changed status OR if the tween is playing and the current time has changed
                if (newState != sdkTweenComponent.TweenStateStatus)
                {
                    sdkTweenComponent.TweenStateStatus = newState;
                    sdkTweenComponent.IsTweenStateDirty = true;
                }

                if (sdkTweenComponent.isPlaying && !sdkTweenComponent.CurrentTime.Equals(currentTime))
                {
                    sdkTweenComponent.CurrentTime = currentTime;
                    sdkTweenComponent.IsTweenStateDirty = true;
                }
            }
            else
            {
                PBTween tweenModel = sdkTweenComponent.CurrentTweenModel;
                bool isPlaying = !tweenModel.HasPlaying || tweenModel.Playing;
                sdkTweenComponent.isPlaying = isPlaying;

                Transform entityTransform = transformComponent.Transform;
                float durationInSeconds = tweenModel.Duration / MILLISECONDS_CONVERSION_INT;

                tempTweener = sdkTweenComponent.Tweener;

                //NOTE: Left this per legacy reasons, Im not sure if this can happen in new renderer
                // There may be a tween running for the entity transform, even though internalComponentModel.tweener
                // is null, e.g: during preview mode hot-reload.
                List<DG.Tweening.Tween> transformTweens = DOTween.TweensByTarget(entityTransform, true);
                transformTweens?[0].Rewind(false);

                if (!EASING_FUNCTIONS_MAP.TryGetValue(tweenModel.EasingFunction, out Ease ease))
                    ease = Linear;

                switch (tweenModel.ModeCase)
                {
                    case PBTween.ModeOneofCase.Rotate:
                        tempTweener = SetupRotationTween(transformComponent.Transform,
                            PBQuaternionToUnityQuaternion(tweenModel.Rotate.Start),
                            PBQuaternionToUnityQuaternion(tweenModel.Rotate.End),
                            durationInSeconds, ease);

                        break;
                    case PBTween.ModeOneofCase.Scale:
                        tempTweener = SetupScaleTween(transformComponent.Transform,
                            PBVectorToUnityVector(tweenModel.Scale.Start),
                            PBVectorToUnityVector(tweenModel.Scale.End),
                            durationInSeconds, ease);

                        break;
                    case PBTween.ModeOneofCase.Move:
                    default:
                        tempTweener = SetupPositionTween(transformComponent.Transform,
                            PBVectorToUnityVector(tweenModel.Move.Start),
                            PBVectorToUnityVector(tweenModel.Move.End),
                            durationInSeconds, ease, tweenModel.Move.HasFaceDirection && tweenModel.Move.FaceDirection);

                        break;
                }

                tempTweener.Goto(tweenModel.CurrentTime * durationInSeconds, isPlaying);

                sdkTweenComponent.Tweener = tempTweener;
                sdkTweenComponent.CurrentTime = tweenModel.CurrentTime;
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

        private static Vector3 PBVectorToUnityVector(Decentraland.Common.Vector3 original) =>
            new ()
            {
                x = original.X,
                y = original.Y,
                z = original.Z,
            };

        private static Quaternion PBQuaternionToUnityQuaternion(Decentraland.Common.Quaternion original) =>
            new ()
            {
                x = original.X,
                y = original.Y,
                z = original.Z,
                w = original.W,
            };

        private Tweener SetupPositionTween(Transform entityTransform, Vector3 startPosition,
            Vector3 endPosition, float durationInSeconds, Ease ease, bool faceDirection)
        {
            if (faceDirection) entityTransform.forward = (endPosition - startPosition).normalized;

            entityTransform.localPosition = startPosition;
            return entityTransform.DOLocalMove(endPosition, durationInSeconds).SetEase(ease).SetAutoKill(false);
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
    }
}
