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
using Scale = UnityEngine.UIElements.Scale;

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

                SetupTweener(ref sdkTweenComponent, entityTransform, pbTween, durationInSeconds, isPlaying, sdkTransform);

                sdkTweenComponent.IsDirty = false;

                if (isPlaying)
                {
                    sdkTweenComponent.CustomTweener.Play();
                    sdkTweenComponent.TweenStateStatus = sdkTweenComponent.CustomTweener.Finished ? TweenStateStatus.TsCompleted : TweenStateStatus.TsActive;
                }
                else
                {
                    sdkTweenComponent.CustomTweener.Pause();
                    sdkTweenComponent.TweenStateStatus = TweenStateStatus.TsPaused;
                }
            }
        }

        private void UpdateTweenState(CRDTEntity sdkEntity, ref SDKTweenComponent sdkTweenComponent, ref SDKTransform sdkTransform)
        {
            var tweenStateDirty = false;
            var newState = GetCurrentTweenState(sdkTweenComponent.CustomTweener.Finished, sdkTweenComponent.IsPlaying);

            //We only update the state if we changed status OR if the tween is playing and the current time has changed
            if (newState != sdkTweenComponent.TweenStateStatus)
            {
                sdkTweenComponent.TweenStateStatus = newState;
                tweenStateDirty = true;
            }

            if (sdkTweenComponent.IsPlaying)
                tweenStateDirty = true;

            if (!tweenStateDirty) return;

            TweenSDKComponentHelper.WriteTweenState(ecsToCRDTWriter, sdkEntity, sdkTweenComponent.TweenStateStatus);
            TweenSDKComponentHelper.WriteTweenTransform(ecsToCRDTWriter, sdkEntity, sdkTweenComponent.CustomTweener);

            var currentResult = sdkTweenComponent.CustomTweener.GetResult();
            sdkTransform.IsDirty = true;
            sdkTransform.Position = currentResult.Position;
            sdkTransform.Rotation = currentResult.Rotation;
            sdkTransform.Scale = currentResult.Scale;
        }

        private void SetupTweener(ref SDKTweenComponent sdkTweenComponent, Transform entityTransform, PBTween tweenModel, float durationInSeconds, bool isPlaying, SDKTransform sdkTransform)
        {
            //NOTE: Left this per legacy reasons, Im not sure if this can happen in new renderer
            // There may be a tween running for the entity transform, e.g: during preview mode hot-reload.
            if (sdkTweenComponent.CustomTweener is { Finished: false })
            {
                sdkTweenComponent.CustomTweener.Rewind();
                var result = sdkTweenComponent.CustomTweener.GetResult();
                entityTransform.transform.position = result.Position;
                entityTransform.transform.rotation = result.Rotation;
                entityTransform.transform.localScale = result.Scale;
            }

            if (!EASING_FUNCTIONS_MAP.TryGetValue(tweenModel.EasingFunction, out Ease ease))
                ease = Linear;

            switch (tweenModel.ModeCase)
            {
                case PBTween.ModeOneofCase.Rotate:
                    sdkTweenComponent.CustomTweener = new RotationTweener(entityTransform,
                        PrimitivesConversionExtensions.PBQuaternionToUnityQuaternion(tweenModel.Rotate.Start),
                        PrimitivesConversionExtensions.PBQuaternionToUnityQuaternion(tweenModel.Rotate.End), durationInSeconds);
                    break;
                case PBTween.ModeOneofCase.Scale:
                    sdkTweenComponent.CustomTweener = new ScaleTweener(entityTransform,
                        PrimitivesConversionExtensions.PBVectorToUnityVector(tweenModel.Scale.Start),
                        PrimitivesConversionExtensions.PBVectorToUnityVector(tweenModel.Scale.End), durationInSeconds);
                    break;
                case PBTween.ModeOneofCase.Move:
                default:
                    var startPosition = PrimitivesConversionExtensions.PBVectorToUnityVector(tweenModel.Move.Start);
                    var endPosition = PrimitivesConversionExtensions.PBVectorToUnityVector(tweenModel.Move.End);

                    if (tweenModel.Move.HasFaceDirection && tweenModel.Move.FaceDirection)
                        entityTransform.forward = (endPosition - startPosition).normalized;
                    sdkTweenComponent.CustomTweener = new PositionTweener(entityTransform, startPosition, endPosition, durationInSeconds);
                    break;
            }

            sdkTweenComponent.CustomTweener.ParentId = sdkTransform.ParentId;
            sdkTweenComponent.CustomTweener.DoTween(ease, tweenModel.CurrentTime * durationInSeconds, isPlaying);
        }

        private void CleanUpTweenBeforeRemoval(CRDTEntity sdkEntity, SDKTweenComponent sdkTweenComponent)
        {
            sdkTweenComponent.Dispose();
            ecsToCRDTWriter.DeleteMessage<PBTweenState>(sdkEntity);
        }

        private TweenStateStatus GetCurrentTweenState(bool finished, bool isPlaying)
        {
            if (!isPlaying) { return TweenStateStatus.TsPaused; }
            return finished ? TweenStateStatus.TsCompleted : TweenStateStatus.TsActive;
        }

    }
}
