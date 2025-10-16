using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using CRDT;
using CrdtEcsBridge.Components.Transform;
using CrdtEcsBridge.ECSToCRDTWriter;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.SDKComponents.Tween.Components;
using DG.Tweening;
using ECS.Abstract;
using ECS.Groups;
using ECS.Unity.Materials.Components;
using ECS.Unity.Transforms.Components;
using ECS.Unity.Transforms.Systems;
using SceneRunner.Scene;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace DCL.SDKComponents.Tween
{
    [UpdateInGroup(typeof(SyncedSimulationSystemGroup))]
    [UpdateBefore(typeof(UpdateTransformSystem))]
    [UpdateAfter(typeof(TweenLoaderSystem))]
    [UpdateAfter(typeof(InstantiateTransformSystem))]
    [LogCategory(ReportCategory.TWEEN)]
    public partial class TweenUpdaterSystem : BaseUnityLoopSystem
    {
        private const int MILLISECONDS_CONVERSION_INT = 1000;

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
            UpdateTweenTransformQuery(World);
            UpdateTweenTextureQuery(World);
        }

        [Query]
        [None(typeof(PBTweenSequence))]
        private void UpdateTweenTransform(ref SDKTweenComponent sdkTweenComponent, ref SDKTransform sdkTransform, in PBTween pbTween, CRDTEntity sdkEntity, TransformComponent transformComponent)
        {
            if (pbTween.ModeCase is PBTween.ModeOneofCase.TextureMove or PBTween.ModeOneofCase.TextureMoveContinuous) return;

            if (sdkTweenComponent.IsDirty)
            {
                SetupTween(ref sdkTweenComponent, in pbTween, transformComponent.Transform);
                UpdateTweenStateAndPosition(sdkEntity, sdkTweenComponent, ref sdkTransform, transformComponent, sceneStateProvider.IsCurrent);
            }
            else
            {
                UpdateTweenState(ref sdkTweenComponent, ref sdkTransform, sdkEntity, transformComponent);
            }
        }

        [Query]
        [None(typeof(PBTweenSequence))]
        private void UpdateTweenTexture(CRDTEntity sdkEntity, in PBTween pbTween, ref SDKTweenComponent sdkTweenComponent, ref MaterialComponent materialComponent)
        {
            if (pbTween.ModeCase != PBTween.ModeOneofCase.TextureMove && pbTween.ModeCase != PBTween.ModeOneofCase.TextureMoveContinuous) return;

            if (sdkTweenComponent.IsDirty)
            {
                SetupTween(ref sdkTweenComponent, in pbTween, null, materialComponent);
                UpdateTweenTextureStateAndMaterial(sdkEntity, sdkTweenComponent, ref materialComponent,
                    pbTween.ModeCase == PBTween.ModeOneofCase.TextureMove ? pbTween.TextureMove.MovementType
                        : pbTween.TextureMoveContinuous.MovementType);
            }
            else
            {
                UpdateTweenTextureState(sdkEntity, ref sdkTweenComponent, ref materialComponent,
                    pbTween.ModeCase == PBTween.ModeOneofCase.TextureMove ? pbTween.TextureMove.MovementType
                        : pbTween.TextureMoveContinuous.MovementType);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UpdateTweenTextureState(CRDTEntity sdkEntity, ref SDKTweenComponent sdkTweenComponent, ref MaterialComponent materialComponent, TextureMovementType movementType)
        {
            TweenStateStatus newState = TweenSDKComponentHelper.GetTweenerState(sdkTweenComponent.CustomTweener);

            if (newState != sdkTweenComponent.TweenStateStatus)
            {
                sdkTweenComponent.TweenStateStatus = newState;
                UpdateTweenTextureStateAndMaterial(sdkEntity, sdkTweenComponent, ref materialComponent, movementType);
            }
            else if (newState == TweenStateStatus.TsActive)
            {
                UpdateTweenMaterial(sdkTweenComponent, ref materialComponent, movementType, sceneStateProvider.IsCurrent);
            }
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

        private void SetupTween(ref SDKTweenComponent sdkTweenComponent, in PBTween pbTween, Transform? transform = null, MaterialComponent? materialComponent = null)
        {
            bool isPlaying = !pbTween.HasPlaying || pbTween.Playing;
            float durationInSeconds = pbTween.Duration / MILLISECONDS_CONVERSION_INT;

            SetupTweener(ref sdkTweenComponent, in pbTween, durationInSeconds, isPlaying, transform, materialComponent);

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
        private void UpdateTweenState(ref SDKTweenComponent sdkTweenComponent, ref SDKTransform sdkTransform, CRDTEntity sdkEntity, TransformComponent transformComponent)
        {
            TweenStateStatus newState = TweenSDKComponentHelper.GetTweenerState(sdkTweenComponent.CustomTweener);
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

        private void SetupTweener(ref SDKTweenComponent sdkTweenComponent, in PBTween tweenModel, float durationInSeconds, bool isPlaying, Transform? transform, MaterialComponent? materialComponent)
        {
            tweenerPool.ReleaseCustomTweenerFrom(sdkTweenComponent);

            Ease ease = IsTweenContinuous(tweenModel) ? Ease.Linear : TweenSDKComponentHelper.GetEase(tweenModel.EasingFunction);

            sdkTweenComponent.TweenMode = tweenModel.ModeCase;
            Vector2? textureStart = null;
            if (tweenModel.ModeCase == PBTween.ModeOneofCase.TextureMoveContinuous)
            {
                textureStart = materialComponent.HasValue && materialComponent.Value.Result ?
                        materialComponent.Value.Result!.mainTextureOffset : Vector2.zero;
            }

            sdkTweenComponent.CustomTweener = tweenerPool.GetTweener(tweenModel, durationInSeconds, transform, textureStart);
            sdkTweenComponent.CustomTweener.DoTween(ease, Mathf.Clamp(tweenModel.CurrentTime, 0f, 1f) * durationInSeconds, isPlaying);
        }

        [Query]
        [None(typeof(PBTweenSequence))]
        private static void UpdatePBTween(ref PBTween pbTween, ref SDKTweenComponent sdkTweenComponent)
        {
            if (pbTween.ModeCase == PBTween.ModeOneofCase.None) return;

            if (pbTween.IsDirty)
                sdkTweenComponent.IsDirty = true;

            // If duration is finite and we've reached the end of the timeline, kill the continuous tween
            if (IsTweenContinuous(pbTween) && TweenSurpassedDuration(pbTween, sdkTweenComponent))
                sdkTweenComponent.CustomTweener.Kill(true);
        }

        private static bool IsTweenContinuous(in PBTween pbTween) =>
            pbTween.ModeCase == PBTween.ModeOneofCase.RotateContinuous ||
            pbTween.ModeCase == PBTween.ModeOneofCase.MoveContinuous ||
            pbTween.ModeCase == PBTween.ModeOneofCase.TextureMoveContinuous;

        private static bool TweenSurpassedDuration(in PBTween pbTween, in SDKTweenComponent sdkTweenComponent) =>
            pbTween.Duration > 0
            && sdkTweenComponent.CustomTweener != null
            && !sdkTweenComponent.CustomTweener.IsFinished()
            && sdkTweenComponent.CustomTweener.GetElapsedTime() >= (pbTween.Duration / MILLISECONDS_CONVERSION_INT);
    }
}
