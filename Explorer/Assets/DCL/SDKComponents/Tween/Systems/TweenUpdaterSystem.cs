using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using CRDT;
using CrdtEcsBridge.ECSToCRDTWriter;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.SDKComponents.Tween.Components;
using DCL.SDKComponents.Tween.Helpers;
using DG.Tweening;
using ECS.Abstract;
using ECS.LifeCycle;
using ECS.LifeCycle.Components;
using ECS.Unity.Transforms.Components;
using System.Collections.Generic;
using CrdtEcsBridge.Components.Transform;
using ECS.Groups;
using ECS.Unity.Materials.Components;
using ECS.Unity.Transforms.Systems;
using SceneRunner.Scene;
using static DCL.ECSComponents.EasingFunction;
using static DG.Tweening.Ease;

namespace DCL.SDKComponents.Tween.Systems
{
    [UpdateInGroup(typeof(SyncedSimulationSystemGroup))]
    [UpdateBefore(typeof(UpdateTransformSystem))]
    [UpdateAfter(typeof(TweenLoaderSystem))]
    [UpdateAfter(typeof(InstantiateTransformSystem))]
    [LogCategory(ReportCategory.TWEEN)]
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
        private readonly TweenerPool tweenerPool;
        private readonly IECSToCRDTWriter ecsToCRDTWriter;
        private readonly ISceneStateProvider sceneStateProvider;

        public TweenUpdaterSystem(World world, IECSToCRDTWriter ecsToCRDTWriter, TweenerPool tweenerPool, ISceneStateProvider sceneStateProvider) : base(world)
        {
            this.tweenerPool = tweenerPool;
            this.ecsToCRDTWriter = ecsToCRDTWriter;
            this.sceneStateProvider = sceneStateProvider;
        }

        protected override void Update(float t)
        {
            UpdatePBTweenQuery(World);

            UpdateTweenTransformSequenceQuery(World);
            UpdateTweenTextureSequenceQuery(World);

            HandleEntityDestructionQuery(World);
            HandleComponentRemovalQuery(World);

            World.Remove<SDKTweenComponent>(in HandleEntityDestruction_QueryDescription);
            World.Remove<SDKTweenComponent>(in HandleComponentRemoval_QueryDescription);
        }

#region MyRegion
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
        private void UpdatePBTween(ref PBTween pbTween, ref SDKTweenComponent sdkTweenComponent)
        {
            if (pbTween.ModeCase == PBTween.ModeOneofCase.None) return;

            if (pbTween.IsDirty)
                sdkTweenComponent.IsDirty = true;
        }
#endregion

        [Query]
        [All(typeof(SDKTweenTextureComponent))]
        private void UpdateTweenTextureSequence(in PBTween pbTween,ref SDKTweenComponent sdkTweenComponent, ref MaterialComponent materialComponent)
        {
            if (sdkTweenComponent.IsDirty)
                SetupTween(ref sdkTweenComponent, in pbTween);
            else
                UpdateTweenTextureState(ref sdkTweenComponent, ref materialComponent);
        }

        private void UpdateTweenTextureState( ref SDKTweenComponent sdkTweenComponent, ref MaterialComponent materialComponent)
        {
            TweenStateStatus newState = GetCurrentTweenState(sdkTweenComponent);

            if (newState != sdkTweenComponent.TweenStateStatus)
            {
                sdkTweenComponent.TweenStateStatus = newState;
                UpdateTweenMaterial(sdkTweenComponent, ref materialComponent, sceneStateProvider.IsCurrent);
            }
            else if (newState == TweenStateStatus.TsActive)
            {
                UpdateTweenMaterial(sdkTweenComponent, ref materialComponent, sceneStateProvider.IsCurrent);
            }
        }

        private void UpdateTweenMaterial( SDKTweenComponent sdkTweenComponent,
            ref MaterialComponent materialComponent,
            bool isInCurrentScene)
        {
            TweenSDKComponentHelper.UpdateTweenResult(sdkTweenComponent.CustomTweener, ref materialComponent, isInCurrentScene);
        }

        [Query]
        [None(typeof(SDKTweenTextureComponent))]
        private void UpdateTweenTransformSequence(ref SDKTweenComponent sdkTweenComponent, ref SDKTransform sdkTransform, in PBTween pbTween, CRDTEntity sdkEntity, TransformComponent transformComponent)
        {
            if (sdkTweenComponent.IsDirty)
            {
                LegacySetupSupport(sdkTweenComponent, ref sdkTransform, ref transformComponent, sdkEntity, sceneStateProvider.IsCurrent);
                SetupTween(ref sdkTweenComponent, in pbTween);
                UpdateTweenStateAndPosition(sdkEntity, sdkTweenComponent, ref sdkTransform, transformComponent, sceneStateProvider.IsCurrent);
            }
            else
                UpdateTweenState(ref sdkTweenComponent, ref sdkTransform, sdkEntity, transformComponent);
        }

        private void SetupTween(ref SDKTweenComponent sdkTweenComponent, in PBTween pbTween)
        {
            bool isPlaying = !pbTween.HasPlaying || pbTween.Playing;
            float durationInSeconds = pbTween.Duration / MILLISECONDS_CONVERSION_INT;

            SetupTweener(ref sdkTweenComponent, in pbTween, durationInSeconds, isPlaying);

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

            sdkTweenComponent.IsDirty = false;
        }

        private void UpdateTweenState(ref SDKTweenComponent sdkTweenComponent, ref SDKTransform sdkTransform, CRDTEntity sdkEntity, TransformComponent transformComponent)
        {
            TweenStateStatus newState = GetCurrentTweenState(sdkTweenComponent);

            if (newState != sdkTweenComponent.TweenStateStatus)
            {
                sdkTweenComponent.TweenStateStatus = newState;
                UpdateTweenStateAndPosition(sdkEntity, sdkTweenComponent, ref sdkTransform, transformComponent, sceneStateProvider.IsCurrent);
            }
            else if (newState == TweenStateStatus.TsActive)
            {
                UpdateTweenPosition(sdkEntity, sdkTweenComponent, ref sdkTransform, transformComponent, sceneStateProvider.IsCurrent);
            }
        }

        private void UpdateTweenStateAndPosition(CRDTEntity sdkEntity, SDKTweenComponent sdkTweenComponent, ref SDKTransform sdkTransform, TransformComponent transformComponent, bool isInCurrentScene)
        {
            UpdateTweenPosition(sdkEntity, sdkTweenComponent, ref sdkTransform, transformComponent, isInCurrentScene);
            TweenSDKComponentHelper.WriteTweenStateInCRDT(ecsToCRDTWriter, sdkEntity, sdkTweenComponent.TweenStateStatus);
        }

        private void UpdateTweenPosition(CRDTEntity sdkEntity, SDKTweenComponent sdkTweenComponent, ref SDKTransform sdkTransform, TransformComponent transformComponent, bool isInCurrentScene)
        {
            TweenSDKComponentHelper.UpdateTweenResult(ref sdkTransform, ref transformComponent, sdkTweenComponent.CustomTweener, isInCurrentScene);
            TweenSDKComponentHelper.WriteSDKTransformUpdateInCRDT(sdkTransform, ecsToCRDTWriter, sdkEntity);
        }

        private void SetupTweener(ref SDKTweenComponent sdkTweenComponent, in PBTween tweenModel, float durationInSeconds, bool isPlaying)
        {
            ReturnTweenToPool(ref sdkTweenComponent);

            Ease ease = EASING_FUNCTIONS_MAP.GetValueOrDefault(tweenModel.EasingFunction, Linear);

            sdkTweenComponent.CustomTweener = tweenerPool.GetTweener(tweenModel, durationInSeconds);
            sdkTweenComponent.CustomTweener.DoTween(ease, tweenModel.CurrentTime * durationInSeconds, isPlaying);
        }

        private void LegacySetupSupport(SDKTweenComponent sdkTweenComponent, ref SDKTransform sdkTransform,
            ref TransformComponent transformComponent, CRDTEntity entity, bool isInCurrentScene)
        {
            //NOTE: Left this per legacy reasons, I'm not sure if this can happen in new renderer
            // There may be a tween running for the entity transform, e.g: during preview mode hot-reload.
            if (sdkTweenComponent.IsActive())
            {
                sdkTweenComponent.Rewind();
                TweenSDKComponentHelper.UpdateTweenResult(ref sdkTransform, ref transformComponent, sdkTweenComponent.CustomTweener, isInCurrentScene);
                TweenSDKComponentHelper.WriteSDKTransformUpdateInCRDT(sdkTransform, ecsToCRDTWriter, entity);
            }
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
