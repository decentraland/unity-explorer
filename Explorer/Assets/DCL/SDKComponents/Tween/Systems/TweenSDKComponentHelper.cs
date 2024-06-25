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
        public static void WriteTweenState(IECSToCRDTWriter ecsToCrdtWriter, CRDTEntity sdkEntity, TweenStateStatus tweenStateStatus)
        {
            ecsToCrdtWriter.PutMessage<PBTweenState, TweenStateStatus>(
                static (component, tweenStateStatus) => component.State = tweenStateStatus, sdkEntity, tweenStateStatus);
        }

        public static void WriteTweenTransform(IECSToCRDTWriter ecsToCrdtWriter, CRDTEntity sdkEntity, ICustomTweener customTweener)
        {
            ecsToCrdtWriter.PutMessage<SDKTransform, ICustomTweener>(
                static (component, customTweener) =>
                {
                    var currentResult = customTweener.GetResult();
                    component.IsDirty = true;
                    component.Position = currentResult.Item1;
                    component.Rotation = currentResult.Item2;
                    component.Scale = currentResult.Item3;
                    component.ParentId = customTweener.ParentId;
                }, sdkEntity, customTweener);
        }

        public static bool AreSameModels(PBTween modelA, PBTween modelB)
        {
            if (modelA == null)
                return false;

            if (modelB.ModeCase != modelA.ModeCase
                || modelB.EasingFunction != modelA.EasingFunction
                || !modelB.CurrentTime.Equals(modelA.CurrentTime)
                || !modelB.Duration.Equals(modelA.Duration)
                || !(!modelB.HasPlaying || modelB.Playing).Equals(!modelA.HasPlaying || modelA.Playing))
                return false;

            return modelA.ModeCase switch
                   {
                       PBTween.ModeOneofCase.Scale => modelB.Scale.Start.Equals(modelA.Scale.Start) && modelB.Scale.End.Equals(modelA.Scale.End),
                       PBTween.ModeOneofCase.Rotate => modelB.Rotate.Start.Equals(modelA.Rotate.Start) && modelB.Rotate.End.Equals(modelA.Rotate.End),
                       PBTween.ModeOneofCase.Move => modelB.Move.Start.Equals(modelA.Move.Start) && modelB.Move.End.Equals(modelA.Move.End),
                       PBTween.ModeOneofCase.None => modelB.Move.Start.Equals(modelA.Move.Start) && modelB.Move.End.Equals(modelA.Move.End),
                       _ => modelB.Move.Start.Equals(modelA.Move.Start) && modelB.Move.End.Equals(modelA.Move.End),
                   };
        }

    }
}
