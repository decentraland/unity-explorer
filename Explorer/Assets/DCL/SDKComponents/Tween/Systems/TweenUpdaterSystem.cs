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
using ECS.Unity.Transforms.Components;
using System.Collections.Generic;
using CrdtEcsBridge.Components.Transform;
using DCL.SDKComponents.Tween.Playground;
using ECS.Groups;
using ECS.Unity.Materials.Components;
using ECS.Unity.Transforms.Systems;
using SceneRunner.Scene;
using System.Runtime.CompilerServices;
using UnityEngine;
using static DCL.ECSComponents.EasingFunction;
using static DG.Tweening.Ease;

namespace DCL.SDKComponents.Tween.Systems
{
    [UpdateInGroup(typeof(SyncedSimulationSystemGroup))]
    [UpdateBefore(typeof(UpdateTransformSystem))]
    [UpdateAfter(typeof(TweenLoaderSystem))]
    [UpdateAfter(typeof(InstantiateTransformSystem))]
    [LogCategory(ReportCategory.TWEEN)]
    public partial class TweenUpdaterSystem : BaseUnityLoopSystem
    {
        private const float MS_TO_SEC = 1f/1000f;

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
        private readonly INtpTimeService ntpTimeService;

        public TweenUpdaterSystem(World world, IECSToCRDTWriter ecsToCRDTWriter, TweenerPool tweenerPool,
            ISceneStateProvider sceneStateProvider, INtpTimeService ntpTimeService) : base(world)
        {
            this.tweenerPool = tweenerPool;
            this.ecsToCRDTWriter = ecsToCRDTWriter;
            this.sceneStateProvider = sceneStateProvider;
            this.ntpTimeService = ntpTimeService;
        }

        protected override void Update(float t)
        {
            UpdatePBTweenQuery(World);
            UpdateTweenTransformSequenceQuery(World);
            UpdateTweenTextureSequenceQuery(World);
        }

        [Query]
        private static void UpdatePBTween(ref PBTween pbTween, ref SDKTweenComponent sdkTweenComponent)
        {
            if (pbTween.ModeCase == PBTween.ModeOneofCase.None) return;

            if (pbTween.IsDirty)
                sdkTweenComponent.IsDirty = true;
        }

        [Query]
        private void UpdateTweenTransformSequence(ref SDKTweenComponent sdkTweenComponent, ref SDKTransform sdkTransform, in PBTween pbTween, CRDTEntity sdkEntity, TransformComponent transformComponent)
        {
            if (pbTween.ModeCase == PBTween.ModeOneofCase.TextureMove) return;

            if (sdkTweenComponent.IsDirty)
            {
                LegacySetupSupport(sdkTweenComponent, ref sdkTransform, ref transformComponent, sdkEntity, sceneStateProvider.IsCurrent);
                SetupTween(ref sdkTweenComponent, in pbTween);
                UpdateTweenStateAndPosition(sdkEntity, sdkTweenComponent, ref sdkTransform, transformComponent, sceneStateProvider.IsCurrent);
            }
            else
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
        }

        [Query]
        private void UpdateTweenTextureSequence(CRDTEntity sdkEntity, in PBTween pbTween, ref SDKTweenComponent sdkTweenComponent, ref MaterialComponent materialComponent)
        {
            if (pbTween.ModeCase != PBTween.ModeOneofCase.TextureMove) return;

            if (sdkTweenComponent.IsDirty)
            {
                SetupTween(ref sdkTweenComponent, in pbTween);
                UpdateTweenTextureStateAndMaterial(sdkEntity, sdkTweenComponent, ref materialComponent, pbTween.TextureMove.MovementType);
            }
            else
                UpdateTweenTextureState(sdkEntity, ref sdkTweenComponent, ref materialComponent, pbTween.TextureMove.MovementType);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UpdateTweenTextureState(CRDTEntity sdkEntity, ref SDKTweenComponent sdkTweenComponent, ref MaterialComponent materialComponent, TextureMovementType movementType)
        {
            TweenStateStatus newState = GetCurrentTweenState(sdkTweenComponent);

            if (newState != sdkTweenComponent.TweenStateStatus)
            {
                sdkTweenComponent.TweenStateStatus = newState;
                UpdateTweenTextureStateAndMaterial(sdkEntity, sdkTweenComponent, ref materialComponent, movementType);
            }
            else if (newState == TweenStateStatus.TsActive) { UpdateTweenMaterial(sdkTweenComponent, ref materialComponent, movementType, sceneStateProvider.IsCurrent); }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UpdateTweenTextureStateAndMaterial(CRDTEntity sdkEntity, SDKTweenComponent sdkTweenComponent, ref MaterialComponent materialComponent, TextureMovementType movementType)
        {
            UpdateTweenMaterial(sdkTweenComponent, ref materialComponent, movementType, sceneStateProvider.IsCurrent);
            TweenSDKComponentHelper.WriteTweenStateInCRDT(ecsToCRDTWriter, sdkEntity, sdkTweenComponent.TweenStateStatus);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void UpdateTweenMaterial(SDKTweenComponent sdkTweenComponent, ref MaterialComponent materialComponent, TextureMovementType movementType, bool isInCurrentScene)
        {
            if (materialComponent.Result)
                TweenSDKComponentHelper.UpdateTweenResult(sdkTweenComponent, ref materialComponent, movementType, isInCurrentScene);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SetupTween(ref SDKTweenComponent sdkTweenComponent, in PBTween pbTween)
        {
            // Don't start the tween that is in the "future"
            if (pbTween.HasStartSyncedTimestamp && pbTween.StartSyncedTimestamp > ntpTimeService.ServerTimeMs)
                return;

            bool isPlaying = !pbTween.HasPlaying || pbTween.Playing;
            float durationInSeconds = pbTween.Duration * MS_TO_SEC;

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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UpdateTweenStateAndPosition(CRDTEntity sdkEntity, SDKTweenComponent sdkTweenComponent, ref SDKTransform sdkTransform, TransformComponent transformComponent, bool isInCurrentScene)
        {
            UpdateTweenPosition(sdkEntity, sdkTweenComponent, ref sdkTransform, transformComponent, isInCurrentScene);
            TweenSDKComponentHelper.WriteTweenStateInCRDT(ecsToCRDTWriter, sdkEntity, sdkTweenComponent.TweenStateStatus);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UpdateTweenPosition(CRDTEntity sdkEntity, SDKTweenComponent sdkTweenComponent, ref SDKTransform sdkTransform, TransformComponent transformComponent, bool isInCurrentScene)
        {
            TweenSDKComponentHelper.UpdateTweenResult(ref sdkTransform, ref transformComponent, sdkTweenComponent, isInCurrentScene);
            TweenSDKComponentHelper.WriteSDKTransformUpdateInCRDT(sdkTransform, ecsToCRDTWriter, sdkEntity);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SetupTweener(ref SDKTweenComponent sdkTweenComponent, in PBTween tweenModel, float durationInSeconds, bool isPlaying)
        {
            tweenerPool.ReleaseCustomTweenerFrom(sdkTweenComponent);

            Ease ease = EASING_FUNCTIONS_MAP.GetValueOrDefault(tweenModel.EasingFunction, Linear);

            sdkTweenComponent.TweenMode = tweenModel.ModeCase;
            sdkTweenComponent.CustomTweener = tweenerPool.GetTweener(tweenModel, durationInSeconds);

            var startTime = tweenModel.CurrentTime * durationInSeconds;

            if (tweenModel.HasStartSyncedTimestamp)
            {
                startTime += (ntpTimeService.ServerTimeMs - tweenModel.StartSyncedTimestamp) * MS_TO_SEC;
                startTime = Mathf.Clamp(startTime, 0, durationInSeconds);
            }

            sdkTweenComponent.CustomTweener.DoTween(ease, startTime, isPlaying);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void LegacySetupSupport(SDKTweenComponent sdkTweenComponent, ref SDKTransform sdkTransform,
            ref TransformComponent transformComponent, CRDTEntity entity, bool isInCurrentScene)
        {
            //NOTE: Left this per legacy reasons, I'm not sure if this can happen in new renderer
            // There may be a tween running for the entity transform, e.g: during preview mode hot-reload.
            if (sdkTweenComponent.IsActive())
            {
                sdkTweenComponent.Rewind();
                TweenSDKComponentHelper.UpdateTweenResult(ref sdkTransform, ref transformComponent, sdkTweenComponent, isInCurrentScene);
                TweenSDKComponentHelper.WriteSDKTransformUpdateInCRDT(sdkTransform, ecsToCRDTWriter, entity);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static TweenStateStatus GetCurrentTweenState(SDKTweenComponent tweener)
        {
            if (tweener.CustomTweener.IsFinished()) return TweenStateStatus.TsCompleted;
            if (tweener.CustomTweener.IsPaused()) return TweenStateStatus.TsPaused;
            return TweenStateStatus.TsActive;
        }
    }
}
