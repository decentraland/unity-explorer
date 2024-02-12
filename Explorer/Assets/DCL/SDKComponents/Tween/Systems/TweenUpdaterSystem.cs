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
using UnityEngine.Pool;
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
        private Sequence currentTweener;

        public TweenUpdaterSystem(World world, WorldProxy globalWorld) : base(world)
        {
            this.globalWorld = globalWorld;
        }

        protected override void Update(float t)
        {
            SetupTweenSequenceQuery(World);
        }

        public void FinalizeComponents(in Query query)
        {
        }

        [Query]
        [All(typeof(PBTween))]
        private void SetupTweenSequence(in Entity entity, ref SDKTweenComponent sdkTweenComponent, ref TransformComponent transformComponent, ref PBTweenState pbTweenState, ref PBTween pbTween)
        {
            if (!sdkTweenComponent.dirty)
            {
                //We update transform positions
                //We also check and update PBTweenState
                //We also need to check playing property on pbTween
            }
            else
            {
                foreach (var tweenModel in tweenModelList)
                {
                    Transform entityTransform = transformComponent.Transform;
                    float durationInSeconds = tweenModel.Duration / 1000;
                    currentTweener = sdkTweenComponent.tweener;

                    if (currentTweener == null) //Not sure if this is really needed at this point.
                    {
                        // There may be a tween running for the entity transform, even though internalComponentModel.tweener
                        // is null, e.g: during preview mode hot-reload.
                        List<DG.Tweening.Tween> transformTweens = DOTween.TweensByTarget(entityTransform, true);
                        transformTweens?[0].Rewind(false);
                        currentTweener = DOTween.Sequence(entityTransform);
                    }
                    else { currentTweener.Rewind(false); }

                    sdkTweenComponent.transform = entityTransform;
                    sdkTweenComponent.currentTime = tweenModel.CurrentTime;

                    if (!EASING_FUNCTIONS_MAP.TryGetValue(tweenModel.EasingFunction, out Ease ease))
                        ease = Linear;

                    switch (tweenModel.ModeCase)
                    {
                        case PBTween.ModeOneofCase.Rotate:
                            currentTweener.Append(SetupRotationTween(transformComponent,
                                PBQuaternionToUnityQuaternion(tweenModel.Rotate.Start),
                                PBQuaternionToUnityQuaternion(tweenModel.Rotate.End),
                                durationInSeconds, ease));

                            break;
                        case PBTween.ModeOneofCase.Scale:
                            currentTweener.Append(SetupScaleTween(ref transformComponent,
                                PBVectorToUnityVector(tweenModel.Scale.Start),
                                PBVectorToUnityVector(tweenModel.Scale.End),
                                durationInSeconds, ease));

                            break;
                        case PBTween.ModeOneofCase.Move:
                        default:
                            currentTweener.Append(SetupPositionTween(transformComponent,
                                PBVectorToUnityVector(tweenModel.Move.Start),
                                PBVectorToUnityVector(tweenModel.Move.End),
                                durationInSeconds, ease, tweenModel.Move.HasFaceDirection && tweenModel.Move.FaceDirection));

                            break;
                    }
                }

                currentTweener.Goto(sdkTweenComponent.currentTweenModel.CurrentTime * sdkTweenComponent.currentTweenModel.Duration / 1000, sdkTweenComponent.playing);

                ListPool<PBTween>.Release(tweenModelList);

                if (sdkTweenComponent.currentTweenSequence is { HasLoop: true })
                {
                    switch (sdkTweenComponent.currentTweenSequence.Loop)
                    {
                        case TweenLoop.TlYoyo:
                            currentTweener.SetLoops(-1, LoopType.Yoyo);
                            break;
                        case TweenLoop.TlRestart:
                            currentTweener.SetLoops(-1, LoopType.Restart);
                            break;
                    }
                }

                sdkTweenComponent.tweener = currentTweener;

                sdkTweenComponent.dirty = false;

                if (sdkTweenComponent.playing)
                    sdkTweenComponent.tweener.Play();
                else
                    sdkTweenComponent.tweener.Pause();
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

            //sbcInternalComponent.SetPosition(scene, entity, entityTransform.position);
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
