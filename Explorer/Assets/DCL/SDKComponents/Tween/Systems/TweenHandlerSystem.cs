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

        private static readonly Dictionary<EasingFunction, Ease> easingFunctionsMap = new Dictionary<EasingFunction,Ease>()
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
            [EfEaseback] = InOutBack
        };

        private readonly WorldProxy globalWorld;
        private Tweener currentTweener;

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

        [Query]
        [None(typeof(SDKTweenComponent))]
        private void LoadTween(in Entity entity, ref PBTween pbTween, ref PartitionComponent partitionComponent, ref TransformComponent transformComponent)
        {
            Entity? globalWorldEntity = globalWorld.Create(pbTween, partitionComponent, transformComponent);

            //if (globalWorldEntity.HasValue)
            {
                var model = pbTween;
                //Add tween logic

                if (model.ModeCase == PBTween.ModeOneofCase.None)
            return;

        // by default it's playing
        bool isPlaying = true;//!model.HasPlaying || model.Playing;

        var internalComponentModel = new SDKTweenComponent(globalWorldEntity.Value);
        //if (!AreSameModels(model, internalComponentModel.lastModel))
        {

            Transform entityTransform = transformComponent.Transform;
            float durationInSeconds = model.Duration / 1000;
            currentTweener = internalComponentModel.tweener;

            if (currentTweener == null)
            {
                // There may be a tween running for the entity transform, even though internalComponentModel.tweener
                // is null, e.g: during preview mode hot-reload.
                var transformTweens = DOTween.TweensByTarget(entityTransform, true);
                transformTweens?[0].Rewind(false);
            }
            else
            {
                currentTweener.Rewind(false);
            }

            internalComponentModel.transform = entityTransform;
            internalComponentModel.currentTime = model.CurrentTime;

            if (!easingFunctionsMap.TryGetValue(model.EasingFunction, out Ease ease))
                ease = Ease.Linear;

            switch (model.ModeCase)
            {
                case PBTween.ModeOneofCase.Rotate:
                    currentTweener = SetupRotationTween( entityTransform,
                        PBQuaternionToUnityQuaternion(model.Rotate.Start),
                        PBQuaternionToUnityQuaternion(model.Rotate.End),
                        durationInSeconds, ease);
                    break;
                case PBTween.ModeOneofCase.Scale:
                    currentTweener = SetupScaleTween( entityTransform,
                        PBVectorToUnityVector(model.Scale.Start),
                        PBVectorToUnityVector(model.Scale.End),
                        durationInSeconds, ease);
                    break;
                case PBTween.ModeOneofCase.Move:
                default:
                    currentTweener = SetupPositionTween( entityTransform,
                        PBVectorToUnityVector(model.Move.Start),
                        PBVectorToUnityVector(model.Move.End),
                        durationInSeconds, ease, model.Move.HasFaceDirection && model.Move.FaceDirection);
                    break;
            }

            currentTweener.Goto(model.CurrentTime * durationInSeconds, isPlaying);
            internalComponentModel.tweener = currentTweener;
            internalComponentModel.tweenMode = model.ModeCase;
        }
      //  else if (internalComponentModel.playing == isPlaying)
        {
      //      return;
        }

        internalComponentModel.playing = isPlaying;

        if (isPlaying)
            internalComponentModel.tweener.Play();
        else
            internalComponentModel.tweener.Pause();

        internalComponentModel.lastModel = model;
        //internalTweenComponent.PutFor(scene, entity, internalComponentModel);


        World.Add(entity, internalComponentModel);

            }
        }

        public static Vector3 PBVectorToUnityVector(Decentraland.Common.Vector3 original) =>
            new()
            {
                x = original.X,
                y = original.Y,
                z = original.Z
            };

        public static Quaternion PBQuaternionToUnityQuaternion(Decentraland.Common.Quaternion original) =>
            new()
            {
                x = original.X,
                y = original.Y,
                z = original.Z,
                w = original.W
            };

        private Tweener SetupPositionTween(/*IParcelScene scene,*/ Transform transform, Vector3 startPosition,
            Vector3 endPosition, float durationInSeconds, Ease ease, bool faceDirection)
        {
            var entityTransform = transform;

            if (faceDirection)
                entityTransform.forward = (endPosition - startPosition).normalized;

            entityTransform.localPosition = startPosition;
            var tweener = entityTransform.DOLocalMove(endPosition, durationInSeconds).SetEase(ease).SetAutoKill(false);

            //sbcInternalComponent.SetPosition(scene, entity, entityTransform.position);

            return tweener;
        }


        private Tweener SetupRotationTween(/*IParcelScene scene,*/ Transform transform, Quaternion startRotation,
            Quaternion endRotation, float durationInSeconds, Ease ease)
        {
            var entityTransform = transform;
            entityTransform.localRotation = startRotation;
            var tweener = entityTransform.DOLocalRotateQuaternion(endRotation, durationInSeconds).SetEase(ease).SetAutoKill(false);

            //sbcInternalComponent.OnTransformScaleRotationChanged(scene, entity);

            return tweener;
        }

        private Tweener SetupScaleTween(/*IParcelScene scene,*/ Transform transform, Vector3 startScale,
            Vector3 endScale, float durationInSeconds, Ease ease)
        {
            var entityTransform = transform;
            entityTransform.localScale = startScale;
            var tweener = entityTransform.DOScale(endScale, durationInSeconds).SetEase(ease).SetAutoKill(false);

            //sbcInternalComponent.OnTransformScaleRotationChanged(scene, transform);

            return tweener;
        }


        [Query]
        private void UpdateTween(ref PBTween pbTween, ref SDKTweenComponent sdkTweenComponent)
        {
            if (!pbTween.IsDirty)
                return;

            //globalWorld.Set(sdkTweenComponent.globalWorldEntity, pbTween);
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
