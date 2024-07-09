using CRDT;
using CrdtEcsBridge.Components.Transform;
using CrdtEcsBridge.ECSToCRDTWriter;
using DCL.ECSComponents;
using DCL.SDKComponents.Tween.Components;
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

        public static void WriteTweenResult(ref SDKTransform sdkTransform, (ICustomTweener, CRDTEntity) tweenResult)
        {
            sdkTransform.IsDirty = true;
            sdkTransform.ParentId = tweenResult.Item2;

            var currentResult = tweenResult.Item1.GetResult();
            sdkTransform.Position = currentResult.Position;
            sdkTransform.Rotation = currentResult.Rotation;
            sdkTransform.Scale = currentResult.Scale;
        }

        public static void WriteTweenResultInCRDT(IECSToCRDTWriter ecsToCrdtWriter, CRDTEntity sdkEntity, (ICustomTweener, CRDTEntity) result)
        {
            ecsToCrdtWriter.PutMessage<SDKTransform, (ICustomTweener, CRDTEntity)>(
                static (component, result) => WriteTweenResult(ref component, result),
                sdkEntity, result);
        }

    }
}
