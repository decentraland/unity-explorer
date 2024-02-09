using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using DCL.Diagnostics;
using DCL.ECSComponents;
using ECS.Abstract;
using DCL.Utilities;
using DG.Tweening;
using ECS.LifeCycle;
using ECS.LifeCycle.Components;
using ECS.Prioritization.Components;
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
    [LogCategory(ReportCategory.TWEEN)]
    [ThrottlingEnabled]
    public partial class TweenHandlerSystem : BaseUnityLoopSystem, IFinalizeWorldSystem
    {
        private static readonly Dictionary<EasingFunction, Ease> easingFunctionsMap = new ()
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

        private readonly WorldProxy globalWorld;
        private Sequence currentTweener;

        public TweenHandlerSystem(World world, WorldProxy globalWorld) : base(world)
        {
            this.globalWorld = globalWorld;
        }

        protected override void Update(float t)
        {
            LoadTweenQuery(World);
            UpdateTweenQuery(World);

            HandleComponentRemovalQuery(World);
            HandleEntityDestructionQuery(World);
        }


        //TWO QUERIES, ONE MUST HAVE SEQUENCE, the Other must NOT have sequence

        [Query]
        [None(typeof(SDKTweenComponent))]
        private void LoadTween(in Entity entity, ref PBTween pbTweenA, ref PBTweenSequence pbTweenSequence, ref TransformComponent transformComponent)
        {
            if (!pbTweenA.IsDirty && !pbTweenSequence.IsDirty) return;

            //Entity? globalWorldEntity = globalWorld.Create(pbTween, partitionComponent, transformComponent);

            //if (globalWorldEntity.HasValue)
            {
                pbTweenA.IsDirty = false;
                pbTweenSequence.IsDirty = false;

                if (pbTweenA.ModeCase == PBTween.ModeOneofCase.None) return;

                // by default it's playing
                bool isPlaying = !pbTweenA.HasPlaying || pbTweenA.Playing;

                var tweenComponent = new SDKTweenComponent(entity);
                var tweenList = new List<PBTween>();
                tweenList.Add(pbTweenA);
                tweenList.AddRange(pbTweenSequence.Sequence);

                foreach (var pbTween in tweenList)
                {
                    if (!AreSameModels(pbTween, tweenComponent.lastModel))
                    {
                        Transform entityTransform = transformComponent.Transform;
                        float durationInSeconds = pbTween.Duration / 1000;
                        currentTweener = tweenComponent.tweener;

                        if (currentTweener == null)
                        {
                            // There may be a tween running for the entity transform, even though internalComponentModel.tweener
                            // is null, e.g: during preview mode hot-reload.
                            List<DG.Tweening.Tween> transformTweens = DOTween.TweensByTarget(entityTransform, true);
                            transformTweens?[0].Rewind(false);
                            currentTweener = DOTween.Sequence(entityTransform);
                        }
                        else { currentTweener.Rewind(false); }

                        tweenComponent.transform = entityTransform;
                        tweenComponent.currentTime = pbTween.CurrentTime;

                        if (!easingFunctionsMap.TryGetValue(pbTween.EasingFunction, out Ease ease))
                            ease = Linear;

                        switch (pbTween.ModeCase)
                        {
                            case PBTween.ModeOneofCase.Rotate:
                                currentTweener.Append(SetupRotationTween(transformComponent,
                                    PBQuaternionToUnityQuaternion(pbTween.Rotate.Start),
                                    PBQuaternionToUnityQuaternion(pbTween.Rotate.End),
                                    durationInSeconds, ease));

                                break;
                            case PBTween.ModeOneofCase.Scale:
                                currentTweener.Append(SetupScaleTween(ref transformComponent,
                                    PBVectorToUnityVector(pbTween.Scale.Start),
                                    PBVectorToUnityVector(pbTween.Scale.End),
                                    durationInSeconds, ease));

                                break;
                            case PBTween.ModeOneofCase.Move:
                            default:
                                currentTweener.Append(SetupPositionTween(transformComponent,
                                    PBVectorToUnityVector(pbTween.Move.Start),
                                    PBVectorToUnityVector(pbTween.Move.End),
                                    durationInSeconds, ease, pbTween.Move.HasFaceDirection && pbTween.Move.FaceDirection));
                                break;
                        }

                        currentTweener.Goto(pbTween.CurrentTime * durationInSeconds, isPlaying);
                    }
                }
                if (pbTweenSequence.HasLoop)
                {
                    if (pbTweenSequence.Loop == TweenLoop.TlYoyo)
                    {
                        currentTweener.SetLoops(-1, LoopType.Yoyo);
                    }
                    else if (pbTweenSequence.Loop == TweenLoop.TlRestart)
                    {
                        currentTweener.SetLoops(-1, LoopType.Restart);
                    }
                }
                tweenComponent.tweener = currentTweener;
                tweenComponent.tweenMode = pbTweenA.ModeCase;


                //else if (tweenComponent.playing == isPlaying) { return; }

                tweenComponent.playing = isPlaying;

                if (isPlaying)
                    tweenComponent.tweener.Play();
                else
                    tweenComponent.tweener.Pause();

                tweenComponent.lastModel = pbTweenA;

                World.Add(entity, tweenComponent);
            }
        }

        [Query]
        private void UpdateTween(ref PBTween pbTween, ref SDKTweenComponent sdkTweenComponent, ref TransformComponent transformComponent)
        {
  //          if (!pbTween.IsDirty)
//                return;

            //check if pbTween data changed + Update entity transform data

            if (!pbTween.IsDirty) return;
        }

        private static bool AreSameModels(PBTween modelA, PBTween modelB)
        {
            if (modelB == null || modelA == null)
                return false;

            if (modelB.ModeCase != modelA.ModeCase
                || modelB.EasingFunction != modelA.EasingFunction
                || !modelB.CurrentTime.Equals(modelA.CurrentTime)
                || !modelB.Duration.Equals(modelA.Duration))
                return false;

            return modelA.ModeCase switch
                   {
                       PBTween.ModeOneofCase.Scale => modelB.Scale.Start.Equals(modelA.Scale.Start) && modelB.Scale.End.Equals(modelA.Scale.End),
                       PBTween.ModeOneofCase.Rotate => modelB.Rotate.Start.Equals(modelA.Rotate.Start) && modelB.Rotate.End.Equals(modelA.Rotate.End),
                       PBTween.ModeOneofCase.Move => modelB.Move.Start.Equals(modelA.Move.Start) && modelB.Move.End.Equals(modelA.Move.End),
                       PBTween.ModeOneofCase.None => modelB.Move.Start.Equals(modelA.Move.Start) && modelB.Move.End.Equals(modelA.Move.End),
                       _ => modelB.Move.Start.Equals(modelA.Move.Start) && modelB.Move.End.Equals(modelA.Move.End),
                   };
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

        [Query]
        [None(typeof(PBTween), typeof(DeleteEntityIntention))]
        private void HandleComponentRemoval(in Entity entity, ref SDKTweenComponent tweenComponent)
        {
            // If the component is removed at scene-world, the global-world representation should disappear entirely
            globalWorld.Add(tweenComponent.globalWorldEntity, new DeleteEntityIntention());

            World.Remove<SDKTweenComponent>(entity);
        }

        [Query]
        [All(typeof(DeleteEntityIntention))]
        private void HandleEntityDestruction(in Entity entity, ref SDKTweenComponent sdkTweenComponent)
        {
            World.Remove<SDKTweenComponent>(entity);
            globalWorld.Add(sdkTweenComponent.globalWorldEntity, new DeleteEntityIntention());
        }

        [Query]
        public void FinalizeComponents(ref SDKTweenComponent sdkTweenComponent)
        {
            globalWorld.Add(sdkTweenComponent.globalWorldEntity, new DeleteEntityIntention());
        }

        public void FinalizeComponents(in Query query)
        {
            FinalizeComponentsQuery(World);
        }
    }
}
