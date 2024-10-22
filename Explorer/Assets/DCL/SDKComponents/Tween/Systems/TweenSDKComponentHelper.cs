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

        public static void WriteTweenResult(ref SDKTransform sdkTransform, (ICustomTweener, CRDTEntity, Transform) tweenResult)
        {
            sdkTransform.IsDirty = true;
            sdkTransform.ParentId = tweenResult.Item2;
            tweenResult.Item1.SetResult(ref sdkTransform, tweenResult.Item3);
        }

        public static void WriteTweenResultInCRDT(IECSToCRDTWriter ecsToCrdtWriter, CRDTEntity sdkEntity, (ICustomTweener, CRDTEntity, Transform) result)
        {
            ecsToCrdtWriter.PutMessage<SDKTransform, (ICustomTweener, CRDTEntity, Transform)>(
                static (component, result) => WriteTweenResult(ref component, result),
                sdkEntity, result);
        }

    }
}
