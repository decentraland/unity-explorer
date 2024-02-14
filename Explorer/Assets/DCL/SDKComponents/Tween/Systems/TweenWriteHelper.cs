using CRDT;
using CrdtEcsBridge.Components.Transform;
using CrdtEcsBridge.ECSToCRDTWriter;
using DCL.ECSComponents;
using ECS.Unity.Transforms.Components;
using ECS.Unity.Tween.Components;

namespace ECS.Unity.Tween.Helpers
{
    public static class TweenWriteHelper
    {
        public static void WriteTweenState(IECSToCRDTWriter ecsToCrdtWriter, CRDTEntity sdkEntity, ref SDKTweenComponent tweenComponent)
        {
            ecsToCrdtWriter.PutMessage<PBTweenState, TweenStateStatus>(
                static (component, tweenStateStatus) => component.State = tweenStateStatus, sdkEntity, tweenComponent.TweenStateStatus);
        }

        public static void WriteTweenTransform(IECSToCRDTWriter ecsToCrdtWriter, CRDTEntity sdkEntity, TransformComponent transform)
        {
            ecsToCrdtWriter.PutMessage<SDKTransform, TransformComponent>(
                static (component, tweenStateStatus) =>
                {
                    component.IsDirty = true;
                    component.Position = tweenStateStatus.Transform.localPosition;
                    component.Rotation = tweenStateStatus.Transform.localRotation;
                    component.Scale = tweenStateStatus.Transform.localScale;
                }, sdkEntity, transform);
        }
    }
}
