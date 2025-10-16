using CRDT;
using CrdtEcsBridge.Components.Transform;
using CrdtEcsBridge.ECSToCRDTWriter;
using DCL.ECSComponents;
using DCL.SDKComponents.Tween.Components;
using DG.Tweening;
using ECS.Unity.Materials.Components;
using ECS.Unity.Transforms.Components;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
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
    }
}
