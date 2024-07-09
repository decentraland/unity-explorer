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
using UnityEngine.Pool;
using static DCL.ECSComponents.EasingFunction;
using static DG.Tweening.Ease;

namespace DCL.SDKComponents.Tween.Systems
{
    [UpdateInGroup(typeof(SyncedSimulationSystemGroup))]
    [UpdateBefore(typeof(UpdateTransformSystem))]
    [UpdateAfter(typeof(UpdateTransformSystem))]
    [LogCategory(ReportCategory.TWEEN)]
    [ThrottlingEnabled]
    public partial class TweenUpdaterSystem : BaseUnityLoopSystem, IFinalizeWorldSystem
    {
        private const int MILLISECONDS_CONVERSION_INT = 1000;
        private readonly TweenerPool tweenerPool;
        

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

        public TweenUpdaterSystem(World world, IECSToCRDTWriter ecsToCRDTWriter, TweenerPool tweenerPool) : base(world)
        {
            this.tweenerPool = tweenerPool;
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
        private void FinalizeComponents(CRDTEntity sdkEntity, ref SDKTweenComponent tweenComponent)
        {
            CleanUpTweenBeforeRemoval(sdkEntity, ref tweenComponent);
        }

        [Query]
        [All(typeof(DeleteEntityIntention))]
        private void HandleEntityDestruction(ref SDKTweenComponent tweenComponent, CRDTEntity sdkEntity)
        {
            CleanUpTweenBeforeRemoval(sdkEntity, ref tweenComponent);
        }

        [Query]
        [None(typeof(PBTween), typeof(DeleteEntityIntention))]
        private void HandleComponentRemoval(ref SDKTweenComponent tweenComponent, CRDTEntity sdkEntity)
        {
            CleanUpTweenBeforeRemoval(sdkEntity, ref tweenComponent);
        }

        [Query]
        private void UpdateTweenSequence(in Entity entity, ref SDKTweenComponent sdkTweenComponent, ref SDKTransform sdkTransform, in PBTween pbTween, in TransformComponent transformComponent, CRDTEntity sdkEntity)
        {
            if (sdkTweenComponent.IsDirty)
                SetupTween(in entity, ref sdkTweenComponent, ref sdkTransform, in pbTween, in transformComponent, sdkEntity);
            else
                UpdateTweenState(in entity, ref sdkTweenComponent, ref sdkTransform, sdkEntity);
        }

        private void SetupTween(in Entity entity, ref SDKTweenComponent sdkTweenComponent, ref SDKTransform sdkTransform, in PBTween pbTween, in TransformComponent transformComponent, CRDTEntity sdkEntity)
        {
            bool isPlaying = !pbTween.HasPlaying || pbTween.Playing;
            var entityTransform = transformComponent.Transform;
            float durationInSeconds = pbTween.Duration / MILLISECONDS_CONVERSION_INT;

            SetupTweener(ref sdkTweenComponent, ref sdkTransform, in pbTween, sdkEntity, entityTransform, durationInSeconds, isPlaying);

            if (isPlaying)
            {
                sdkTweenComponent.CustomTweener.Play();
                sdkTweenComponent.TweenStateStatus = TweenStateStatus.TsActive;
            }
            else
            {
                sdkTweenComponent.CustomTweener.Pause();
                sdkTweenComponent.TweenStateStatus = TweenStateStatus.TsPaused;
            }

            UpdateTweenStateAndPosition(entity, sdkEntity, sdkTweenComponent, ref sdkTransform);
            sdkTweenComponent.IsDirty = false;
        }

        private void UpdateTweenState(in Entity entity, ref SDKTweenComponent sdkTweenComponent, ref SDKTransform sdkTransform, CRDTEntity sdkEntity)
        {
            var newState = GetCurrentTweenState(sdkTweenComponent);

            if (newState != sdkTweenComponent.TweenStateStatus)
            {
                sdkTweenComponent.TweenStateStatus = newState;
                UpdateTweenStateAndPosition(entity, sdkEntity, sdkTweenComponent, ref sdkTransform);
            }
            else if (newState == TweenStateStatus.TsActive)
            {
                UpdateTweenPosition(entity, sdkEntity, sdkTweenComponent, ref sdkTransform);
            }
        }

        private void UpdateTweenStateAndPosition(in Entity entity, CRDTEntity sdkEntity, SDKTweenComponent sdkTweenComponent, ref SDKTransform sdkTransform)
        {
            UpdateTweenPosition(entity, sdkEntity, sdkTweenComponent, ref sdkTransform);
            TweenSDKComponentHelper.WriteTweenStateInCRDT(ecsToCRDTWriter, sdkEntity, sdkTweenComponent.TweenStateStatus);
        }

        private void UpdateTweenPosition(in Entity entity,CRDTEntity sdkEntity, SDKTweenComponent sdkTweenComponent, ref SDKTransform sdkTransform)
        {
            TweenSDKComponentHelper.WriteTweenResult(ref sdkTransform, (sdkTweenComponent.CustomTweener, sdkTransform.ParentId));
            TweenSDKComponentHelper.WriteTweenResultInCRDT(ecsToCRDTWriter, sdkEntity, (sdkTweenComponent.CustomTweener, sdkTransform.ParentId));
        }


        private void SetupTweener(ref SDKTweenComponent sdkTweenComponent,  ref SDKTransform sdkTransform, in PBTween tweenModel, CRDTEntity entity, Transform entityTransform, float durationInSeconds, bool isPlaying)
        {
            //NOTE: Left this per legacy reasons, Im not sure if this can happen in new renderer
            // There may be a tween running for the entity transform, e.g: during preview mode hot-reload.
            if (sdkTweenComponent.IsActive())
            {
                sdkTweenComponent.Rewind();
                TweenSDKComponentHelper.WriteTweenResult(ref sdkTransform, (sdkTweenComponent.CustomTweener, sdkTransform.ParentId));
                TweenSDKComponentHelper.WriteTweenResultInCRDT(ecsToCRDTWriter, entity, (sdkTweenComponent.CustomTweener, sdkTransform.ParentId));
            }

            ReturnTweenToPool(ref sdkTweenComponent);

            if (!EASING_FUNCTIONS_MAP.TryGetValue(tweenModel.EasingFunction, out Ease ease))
                ease = Linear;

            sdkTweenComponent.CustomTweener = tweenerPool.GetTweener(tweenModel, entityTransform, durationInSeconds);
            sdkTweenComponent.CustomTweener.DoTween(ease, tweenModel.CurrentTime * durationInSeconds, isPlaying);
        }

        private void CleanUpTweenBeforeRemoval(CRDTEntity sdkEntity, ref SDKTweenComponent sdkTweenComponent)
        {
            ReturnTweenToPool(ref sdkTweenComponent);
            ecsToCRDTWriter.DeleteMessage<PBTweenState>(sdkEntity);
        }

        private void ReturnTweenToPool(ref SDKTweenComponent sdkTweenComponent)
        {
            tweenerPool.Return(sdkTweenComponent);
            sdkTweenComponent.CustomTweener = null;
        }

        private TweenStateStatus GetCurrentTweenState(SDKTweenComponent tweener)
        {
            if (tweener.CustomTweener.IsFinished()) return TweenStateStatus.TsCompleted;
            if (tweener.CustomTweener.IsPaused()) return TweenStateStatus.TsPaused;
            return TweenStateStatus.TsActive;
        }

    }
}
