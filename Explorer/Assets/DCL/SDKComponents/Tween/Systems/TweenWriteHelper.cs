using CRDT;
using CrdtEcsBridge.Components.Transform;
using CrdtEcsBridge.ECSToCRDTWriter;
using DCL.ECSComponents;
using DCL.SDKComponents.Tween.Components;
using ECS.Unity.Transforms.Components;

namespace DCL.SDKComponents.Tween.Helpers
{
    public static class TweenWriteHelper
    {
        public static void WriteTweenState(IECSToCRDTWriter ecsToCrdtWriter, CRDTEntity sdkEntity, TweenStateStatus tweenStateStatus)
        {
            ecsToCrdtWriter.PutMessage<PBTweenState, TweenStateStatus>(
                static (component, tweenStateStatus) => component.State = tweenStateStatus, sdkEntity, tweenStateStatus);
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
