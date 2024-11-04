using CRDT;
using CrdtEcsBridge.Components.Transform;
using CrdtEcsBridge.ECSToCRDTWriter;
using DCL.ECSComponents;
using DCL.SDKComponents.Tween.Components;
using ECS.Unity.Transforms.Components;
using UnityEngine;

namespace DCL.SDKComponents.Tween.Helpers
{
    public static class TweenSDKComponentHelper
    {
        public static void WriteTweenStateInCRDT(IECSToCRDTWriter ecsToCrdtWriter, CRDTEntity sdkEntity, TweenStateStatus tweenStateStatus)
        {
            ecsToCrdtWriter.PutMessage<PBTweenState, TweenStateStatus>(
                static (component, tweenStateStatus) => component.State = tweenStateStatus, sdkEntity, tweenStateStatus);
        }

        public static void UpdateTweenResult(ref SDKTransform sdkTransform, ref TransformComponent transformComponent, ICustomTweener tweener, bool shouldUpdateTransform)
        {
            tweener.UpdateSDKTransform(ref sdkTransform);

            //we only set the SDK transform to dirty here if we didn't already update the transform, but if the sdkTransform was already dirty,
            //we dont change it, as it might have pending updates to be done from the scene side.
            if (shouldUpdateTransform)
            {
                tweener.UpdateTransform(transformComponent.Transform);
                transformComponent.UpdateCache();
            } else
                sdkTransform.IsDirty = true;

        }

        public static void WriteSDKTransformUpdateInCRDT(SDKTransform sdkTransform, IECSToCRDTWriter ecsToCrdtWriter, CRDTEntity sdkEntity)
        {
            ecsToCrdtWriter.PutMessage<SDKTransform, SDKTransform>((component , transform) =>
            {
                component.Position.Value = transform.Position.Value;
                component.ParentId = transform.ParentId;
                component.Rotation.Value = transform.Rotation.Value;
                component.Scale = transform.Scale;
            }, sdkEntity, sdkTransform);
        }
    }
}
