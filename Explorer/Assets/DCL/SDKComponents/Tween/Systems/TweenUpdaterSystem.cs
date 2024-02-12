using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
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
        private readonly WorldProxy globalWorld;
        private Tweener currentTweener;

        public TweenUpdaterSystem(World world, WorldProxy globalWorld) : base(world)
        {
            this.globalWorld = globalWorld;
        }

        protected override void Update(float t)
        {
            SetupTweenSequenceQuery(World);
        }

        public void FinalizeComponents(in Query query) { }

        private TweenStateStatus GetTweenState(float currentTime, bool isPlaying)
        {
            if (!isPlaying) { return TweenStateStatus.TsPaused; }

            return currentTime.Equals(1f) ? TweenStateStatus.TsCompleted : TweenStateStatus.TsActive;
        }

        [Query]
        [All(typeof(SDKTweenComponent))]
        private void SetupTweenSequence(ref SDKTweenComponent sdkTweenComponent, ref TransformComponent transformComponent)
        {
            if (!sdkTweenComponent.IsDirty)
            {
                //We also need to update transform positions
                float currentTime = sdkTweenComponent.tweener.ElapsedPercentage();
                TweenStateStatus newState = GetTweenState(currentTime, sdkTweenComponent.isPlaying);

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
                float durationInSeconds = tweenModel.Duration / 1000;

                currentTweener = sdkTweenComponent.tweener;

                // There may be a tween running for the entity transform, even though internalComponentModel.tweener
                // is null, e.g: during preview mode hot-reload.
                List<DG.Tweening.Tween> transformTweens = DOTween.TweensByTarget(entityTransform, true);
                transformTweens?[0].Rewind(false);

                if (!EASING_FUNCTIONS_MAP.TryGetValue(tweenModel.EasingFunction, out Ease ease))
                    ease = Linear;

                switch (tweenModel.ModeCase)
                {
                    case PBTween.ModeOneofCase.Rotate:
                        currentTweener = SetupRotationTween(transformComponent,
                            PBQuaternionToUnityQuaternion(tweenModel.Rotate.Start),
                            PBQuaternionToUnityQuaternion(tweenModel.Rotate.End),
                            durationInSeconds, ease);

                        break;
                    case PBTween.ModeOneofCase.Scale:
                        currentTweener = SetupScaleTween(ref transformComponent,
                            PBVectorToUnityVector(tweenModel.Scale.Start),
                            PBVectorToUnityVector(tweenModel.Scale.End),
                            durationInSeconds, ease);

                        break;
                    case PBTween.ModeOneofCase.Move:
                    default:
                        currentTweener = SetupPositionTween(transformComponent,
                            PBVectorToUnityVector(tweenModel.Move.Start),
                            PBVectorToUnityVector(tweenModel.Move.End),
                            durationInSeconds, ease, tweenModel.Move.HasFaceDirection && tweenModel.Move.FaceDirection);

                        break;
                }

                currentTweener.Goto(tweenModel.CurrentTime * durationInSeconds, isPlaying);

                sdkTweenComponent.tweener = currentTweener;
                sdkTweenComponent.CurrentTime = tweenModel.CurrentTime;
                sdkTweenComponent.IsDirty = false;

                if (isPlaying)
                {
                    sdkTweenComponent.tweener.Play();
                    sdkTweenComponent.TweenStateStatus = sdkTweenComponent.CurrentTime.Equals(1f) ? TweenStateStatus.TsCompleted : TweenStateStatus.TsActive;
                }
                else
                {
                    sdkTweenComponent.tweener.Pause();
                    sdkTweenComponent.TweenStateStatus = TweenStateStatus.TsPaused;
                }
            }
        }

        public static Vector3 PBVectorToUnityVector(Decentraland.Common.Vector3 original) =>
            new ()
            {
                x = original.X,
                y = original.Y,
                z = original.Z,
            };

        public static Quaternion PBQuaternionToUnityQuaternion(Decentraland.Common.Quaternion original) =>
            new ()
            {
                x = original.X,
                y = original.Y,
                z = original.Z,
                w = original.W,
            };

        private Tweener SetupPositionTween( /*IParcelScene scene,*/ TransformComponent transformComponent, Vector3 startPosition,
            Vector3 endPosition, float durationInSeconds, Ease ease, bool faceDirection)
        {
            Transform entityTransform = transformComponent.Transform;

            if (faceDirection)
                entityTransform.forward = (endPosition - startPosition).normalized;

            entityTransform.localPosition = startPosition;
            return entityTransform.DOLocalMove(endPosition, durationInSeconds).SetEase(ease).SetAutoKill(false);
        }

        private Tweener SetupRotationTween( /*IParcelScene scene,*/ TransformComponent transformComponent, Quaternion startRotation,
            Quaternion endRotation, float durationInSeconds, Ease ease)
        {
            Transform entityTransform = transformComponent.Transform;
            entityTransform.localRotation = startRotation;
            return entityTransform.DOLocalRotateQuaternion(endRotation, durationInSeconds).SetEase(ease).SetAutoKill(false);

            //sbcInternalComponent.OnTransformScaleRotationChanged(scene, entity);
        }

        private Tweener SetupScaleTween( /*IParcelScene scene,*/ ref TransformComponent transformComponent, Vector3 startScale,
            Vector3 endScale, float durationInSeconds, Ease ease)
        {
            Transform entityTransform = transformComponent.Transform;
            entityTransform.localScale = startScale;
            transformComponent.Cached.LocalScale = startScale;
            return entityTransform.DOScale(endScale, durationInSeconds).SetEase(ease).SetAutoKill(false);

            //sbcInternalComponent.OnTransformScaleRotationChanged(scene, transform);
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
