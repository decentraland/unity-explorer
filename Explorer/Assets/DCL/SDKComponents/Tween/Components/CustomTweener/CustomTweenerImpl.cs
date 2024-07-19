using CommunityToolkit.HighPerformance.Helpers;
using CrdtEcsBridge.Components.Conversion;
using CrdtEcsBridge.Components.Transform;
using DCL.ECSComponents;
using DG.Tweening;
using DG.Tweening.Core;
using DG.Tweening.CustomPlugins;
using DG.Tweening.Plugins.Options;
using UnityEngine;

namespace DCL.SDKComponents.Tween.Components
{

    public abstract class Vector3Tweener : CustomTweener<Vector3, VectorOptions>
    {
        protected override TweenerCore<Vector3, Vector3, VectorOptions> CreateTweener(Vector3 start, Vector3 end, float duration)
        {
            return DOTween.To(() => CurrentValue, x => CurrentValue = x, end, duration);
        }
    } 
    
    
    public class PositionTweener : Vector3Tweener
    {
        protected override (Vector3, Vector3) GetTweenValues(PBTween pbTween, Transform startTransform)
        {
            var start = PrimitivesConversionExtensions.PBVectorToUnityVector(pbTween.Move.Start);
            var end = PrimitivesConversionExtensions.PBVectorToUnityVector(pbTween.Move.End);
            startTransform.localPosition = start;
            CurrentValue = start;
            return (start, end);
        }

        public override void SetResult(ref SDKTransform sdkTransform)
        {
            sdkTransform.Position = CurrentValue;
        }
    }

    public class ScaleTweener : Vector3Tweener
    {
        protected override (Vector3, Vector3) GetTweenValues(PBTween pbTween, Transform startTransform)
        {
            var start = PrimitivesConversionExtensions.PBVectorToUnityVector(pbTween.Scale.Start);
            var end = PrimitivesConversionExtensions.PBVectorToUnityVector(pbTween.Scale.End);
            startTransform.localScale = start;
            CurrentValue = start;
            return (start, end);
        }

        public override void SetResult(ref SDKTransform sdkTransform)
        {
            sdkTransform.Scale = CurrentValue;
        }

        protected override TweenerCore<Vector3, Vector3, VectorOptions> CreateTweener(Vector3 start, Vector3 end, float duration)
        {
            return DOTween.To(() => CurrentValue,
                x => CurrentValue = x,
                end, duration);
        }
    }

    public class RotationTweener : CustomTweener<Quaternion, NoOptions>
    {
        protected override TweenerCore<Quaternion, Quaternion, NoOptions> CreateTweener(Quaternion start, Quaternion end, float duration)
        {
            return DOTween.To(PureQuaternionPlugin.Plug(), () => CurrentValue,
                x => CurrentValue = x,
                end, duration);
        }

        protected override (Quaternion, Quaternion) GetTweenValues(PBTween pbTween, Transform startTransform)
        {
            var start = PrimitivesConversionExtensions.PBQuaternionToUnityQuaternion(pbTween.Rotate.Start);
            var end = PrimitivesConversionExtensions.PBQuaternionToUnityQuaternion(pbTween.Rotate.End);
            startTransform.localRotation = start;
            CurrentValue = start;
            return (start, end);
        }

        public override void SetResult(ref SDKTransform sdkTransform)
        {
            sdkTransform.Rotation = CurrentValue;
        }
    }
}