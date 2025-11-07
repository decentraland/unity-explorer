using CRDT;
using CrdtEcsBridge.Components.Transform;
using CrdtEcsBridge.ECSToCRDTWriter;
using DCL.ECSComponents;
using DCL.SDKComponents.Tween.Components;
using DG.Tweening;
using ECS.Unity.Materials.Components;
using ECS.Unity.Transforms.Components;
using SceneRunner.Scene;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using static DCL.ECSComponents.EasingFunction;
using static DG.Tweening.Ease;

namespace DCL.SDKComponents.Tween
{
    public static class TweenSDKComponentHelper
    {
        private static readonly Dictionary<EasingFunction, Ease> EASING_FUNCTIONS_MAP = new()
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

        public static Ease GetEase(EasingFunction easingFunction) =>
            EASING_FUNCTIONS_MAP.GetValueOrDefault(easingFunction, Linear);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TweenStateStatus GetTweenerState(ITweener tweener)
        {
            if (tweener.IsFinished()) return TweenStateStatus.TsCompleted;
            if (tweener.IsPaused()) return TweenStateStatus.TsPaused;
            return TweenStateStatus.TsActive;
        }

        public static void WriteTweenStateInCRDT(IECSToCRDTWriter ecsToCrdtWriter, CRDTEntity sdkEntity, TweenStateStatus tweenStateStatus)
        {
            ecsToCrdtWriter.PutMessage<PBTweenState, TweenStateStatus>(
                static (component, tweenStateStatus) => component.State = tweenStateStatus, sdkEntity, tweenStateStatus);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void UpdateTweenResult(ref SDKTransform sdkTransform, ref TransformComponent transformComponent, SDKTweenComponent sdkTweenComponent, bool shouldUpdateTransform)
        {
            sdkTweenComponent.CustomTweener.UpdateSDKTransform(ref sdkTransform, sdkTweenComponent.TweenMode);

            //we only set the SDK transform to dirty here if we didn't already update the transform, but if the sdkTransform was already dirty,
            //we dont change it, as it might have pending updates to be done from the scene side.
            if (shouldUpdateTransform)
            {
                sdkTweenComponent.CustomTweener.UpdateTransform(transformComponent.Transform, sdkTweenComponent.TweenMode);
                transformComponent.UpdateCache();
            }
            else
                sdkTransform.IsDirty = true;
        }

        public static void UpdateTweenResult(SDKTweenComponent sdkTweenComponent, ref MaterialComponent materialComponent, TextureMovementType movementType, bool shouldUpdateMaterial)
        {
            if (shouldUpdateMaterial)
                sdkTweenComponent.CustomTweener.UpdateMaterial(materialComponent.Result!, movementType);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SyncTransformToSDKTransform(Transform transform, ref SDKTransform sdkTransform, ref TransformComponent transformComponent, bool isInCurrentScene)
        {
            // Read back from Unity Transform (DOTween Sequence updates it directly) and sync to SDKTransform
            sdkTransform.Position.Value = transform.localPosition;
            sdkTransform.Rotation.Value = transform.localRotation;
            sdkTransform.Scale = transform.localScale;

            // Update the transform component cache if we're in the current scene
            if (isInCurrentScene)
                transformComponent.UpdateCache();
            else
                sdkTransform.IsDirty = true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteSDKTransformUpdateInCRDT(SDKTransform sdkTransform, IECSToCRDTWriter ecsToCrdtWriter, CRDTEntity sdkEntity)
        {
            ecsToCrdtWriter.PutMessage<SDKTransform, SDKTransform>((component, transform) =>
            {
                component.Position.Value = transform.Position.Value;
                component.ParentId = transform.ParentId;
                component.Rotation.Value = transform.Rotation.Value;
                component.Scale = transform.Scale;
            }, sdkEntity, sdkTransform);
        }

        private const int MILLISECONDS_CONVERSION_INT = 1000;

        public static void UpdatePBTween(ref PBTween pbTween, ref SDKTweenComponent sdkTweenComponent)
        {
            if (pbTween.ModeCase == PBTween.ModeOneofCase.None) return;

            if (pbTween.IsDirty)
                sdkTweenComponent.IsDirty = true;

            // If duration is finite and we've reached the end of the timeline, kill the continuous tween
            if (IsTweenContinuous(pbTween) && TweenSurpassedDuration(pbTween, sdkTweenComponent))
                sdkTweenComponent.CustomTweener.Kill(true);
        }

        public static void UpdateTweenTransform(ref SDKTweenComponent sdkTweenComponent, ref SDKTransform sdkTransform, in PBTween pbTween, CRDTEntity sdkEntity, TransformComponent transformComponent, TweenerPool tweenerPool, IECSToCRDTWriter ecsToCRDTWriter, ISceneStateProvider sceneStateProvider)
        {
            if (pbTween.ModeCase is PBTween.ModeOneofCase.TextureMove or PBTween.ModeOneofCase.TextureMoveContinuous) return;

            if (sdkTweenComponent.IsDirty)
            {
                SetupTween(ref sdkTweenComponent, in pbTween, transformComponent.Transform, null, tweenerPool);
                UpdateTweenStateAndPosition(sdkEntity, sdkTweenComponent, ref sdkTransform, transformComponent, sceneStateProvider, ecsToCRDTWriter);
            }
            else
            {
                UpdateTweenState(ref sdkTweenComponent, ref sdkTransform, sdkEntity, transformComponent, sceneStateProvider, ecsToCRDTWriter);
            }
        }

        public static void UpdateTweenTexture(CRDTEntity sdkEntity, in PBTween pbTween, ref SDKTweenComponent sdkTweenComponent, ref MaterialComponent materialComponent, TweenerPool tweenerPool, IECSToCRDTWriter ecsToCRDTWriter, ISceneStateProvider sceneStateProvider)
        {
            if (pbTween.ModeCase != PBTween.ModeOneofCase.TextureMove && pbTween.ModeCase != PBTween.ModeOneofCase.TextureMoveContinuous) return;

            if (sdkTweenComponent.IsDirty)
            {
                SetupTween(ref sdkTweenComponent, in pbTween, null, materialComponent, tweenerPool);
                UpdateTweenTextureStateAndMaterial(sdkEntity, sdkTweenComponent, ref materialComponent,
                    pbTween.ModeCase == PBTween.ModeOneofCase.TextureMove ? pbTween.TextureMove.MovementType
                        : pbTween.TextureMoveContinuous.MovementType, sceneStateProvider, ecsToCRDTWriter);
            }
            else
            {
                UpdateTweenTextureState(sdkEntity, ref sdkTweenComponent, ref materialComponent,
                    pbTween.ModeCase == PBTween.ModeOneofCase.TextureMove ? pbTween.TextureMove.MovementType
                        : pbTween.TextureMoveContinuous.MovementType, sceneStateProvider, ecsToCRDTWriter);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void UpdateTweenTextureState(CRDTEntity sdkEntity, ref SDKTweenComponent sdkTweenComponent, ref MaterialComponent materialComponent, TextureMovementType movementType, ISceneStateProvider sceneStateProvider, IECSToCRDTWriter ecsToCRDTWriter)
        {
            TweenStateStatus newState = GetTweenerState(sdkTweenComponent.CustomTweener);

            if (newState != sdkTweenComponent.TweenStateStatus)
            {
                sdkTweenComponent.TweenStateStatus = newState;
                UpdateTweenTextureStateAndMaterial(sdkEntity, sdkTweenComponent, ref materialComponent, movementType, sceneStateProvider, ecsToCRDTWriter);
            }
            else if (newState == TweenStateStatus.TsActive)
            {
                UpdateTweenMaterial(sdkTweenComponent, ref materialComponent, movementType, sceneStateProvider.IsCurrent);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void UpdateTweenTextureStateAndMaterial(CRDTEntity sdkEntity, SDKTweenComponent sdkTweenComponent, ref MaterialComponent materialComponent, TextureMovementType movementType, ISceneStateProvider sceneStateProvider, IECSToCRDTWriter ecsToCRDTWriter)
        {
            UpdateTweenMaterial(sdkTweenComponent, ref materialComponent, movementType, sceneStateProvider.IsCurrent);
            WriteTweenStateInCRDT(ecsToCRDTWriter, sdkEntity, sdkTweenComponent.TweenStateStatus);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void UpdateTweenMaterial(SDKTweenComponent sdkTweenComponent, ref MaterialComponent materialComponent, TextureMovementType movementType, bool isInCurrentScene)
        {
            if (materialComponent.Result)
                UpdateTweenResult(sdkTweenComponent, ref materialComponent, movementType, isInCurrentScene);
        }

        private static void SetupTween(ref SDKTweenComponent sdkTweenComponent, in PBTween pbTween, Transform? transform, MaterialComponent? materialComponent, TweenerPool tweenerPool)
        {
            bool isPlaying = !pbTween.HasPlaying || pbTween.Playing;
            float durationInSeconds = pbTween.Duration / MILLISECONDS_CONVERSION_INT;

            SetupTweener(ref sdkTweenComponent, in pbTween, durationInSeconds, isPlaying, transform, materialComponent, tweenerPool);

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
        private static void UpdateTweenState(ref SDKTweenComponent sdkTweenComponent, ref SDKTransform sdkTransform, CRDTEntity sdkEntity, TransformComponent transformComponent, ISceneStateProvider sceneStateProvider, IECSToCRDTWriter ecsToCRDTWriter)
        {
            TweenStateStatus newState = GetTweenerState(sdkTweenComponent.CustomTweener);
            if (newState != sdkTweenComponent.TweenStateStatus)
            {
                sdkTweenComponent.TweenStateStatus = newState;
                UpdateTweenStateAndPosition(sdkEntity, sdkTweenComponent, ref sdkTransform, transformComponent, sceneStateProvider, ecsToCRDTWriter);
            }
            else if (newState == TweenStateStatus.TsActive)
            {
                UpdateTweenPosition(sdkEntity, sdkTweenComponent, ref sdkTransform, transformComponent, sceneStateProvider.IsCurrent, ecsToCRDTWriter);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void UpdateTweenStateAndPosition(CRDTEntity sdkEntity, SDKTweenComponent sdkTweenComponent, ref SDKTransform sdkTransform, TransformComponent transformComponent, ISceneStateProvider sceneStateProvider, IECSToCRDTWriter ecsToCRDTWriter)
        {
            UpdateTweenPosition(sdkEntity, sdkTweenComponent, ref sdkTransform, transformComponent, sceneStateProvider.IsCurrent, ecsToCRDTWriter);
            WriteTweenStateInCRDT(ecsToCRDTWriter, sdkEntity, sdkTweenComponent.TweenStateStatus);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void UpdateTweenPosition(CRDTEntity sdkEntity, SDKTweenComponent sdkTweenComponent, ref SDKTransform sdkTransform, TransformComponent transformComponent, bool isInCurrentScene, IECSToCRDTWriter ecsToCRDTWriter)
        {
            UpdateTweenResult(ref sdkTransform, ref transformComponent, sdkTweenComponent, isInCurrentScene);
            WriteSDKTransformUpdateInCRDT(sdkTransform, ecsToCRDTWriter, sdkEntity);
        }

        private static void SetupTweener(ref SDKTweenComponent sdkTweenComponent, in PBTween tweenModel, float durationInSeconds, bool isPlaying, Transform? transform, MaterialComponent? materialComponent, TweenerPool tweenerPool)
        {
            tweenerPool.ReleaseCustomTweenerFrom(sdkTweenComponent);

            Ease ease = IsTweenContinuous(tweenModel) ? Ease.Linear : GetEase(tweenModel.EasingFunction);

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
