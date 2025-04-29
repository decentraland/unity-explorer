using CRDT;
using CrdtEcsBridge.Components.Transform;
using CrdtEcsBridge.ECSToCRDTWriter;
using DCL.ECSComponents;
using DCL.SDKComponents.Tween.Components;
using ECS.Unity.Materials.Components;
using ECS.Unity.Transforms.Components;
using System.Runtime.CompilerServices;

namespace DCL.SDKComponents.Tween.Helpers
{
    public static class TweenSDKComponentHelper
    {
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
