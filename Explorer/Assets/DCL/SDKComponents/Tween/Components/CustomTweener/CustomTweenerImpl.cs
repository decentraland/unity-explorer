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
    public class PositionTweener : CustomTweener<Vector3, VectorOptions>
    {
        protected override TweenerCore<Vector3, Vector3, VectorOptions> CreateTweener(Vector3 start, Vector3 end, float duration)
        {
            return DOTween.To(() => currentValue, x => currentValue = x, end, duration);
        }

        protected override (Vector3, Vector3) GetTweenValues(PBTween pbTween, SDKTransform startTransform)
        {
            Vector3 start = PrimitivesConversionExtensions.PBVectorToUnityVector(pbTween.Move.Start);
            Vector3 end = PrimitivesConversionExtensions.PBVectorToUnityVector(pbTween.Move.End);
            startTransform.Position.Value = start;
            currentValue = start;
            return (start, end);
        }

        public override void SetResult(ref SDKTransform sdkTransform)
        {
            sdkTransform.Position.Value = currentValue;
        }
    }

    public class ScaleTweener : CustomTweener<Vector3, VectorOptions>
    {
        protected override TweenerCore<Vector3, Vector3, VectorOptions> CreateTweener(Vector3 start, Vector3 end, float duration)
        {
            return DOTween.To(() => currentValue, x => currentValue = x, end, duration);
        }

        protected override (Vector3, Vector3) GetTweenValues(PBTween pbTween, SDKTransform startTransform)
        {
            Vector3 start = PrimitivesConversionExtensions.PBVectorToUnityVector(pbTween.Scale.Start);
            Vector3 end = PrimitivesConversionExtensions.PBVectorToUnityVector(pbTween.Scale.End);
            startTransform.Scale = start;
            currentValue = start;
            return (start, end);
        }

        public override void SetResult(ref SDKTransform sdkTransform)
        {
            sdkTransform.Scale = currentValue;
        }
    }

    public class RotationTweener : CustomTweener<Quaternion, NoOptions>
    {
        protected override (Quaternion, Quaternion) GetTweenValues(PBTween pbTween, SDKTransform startTransform)
        {
            Quaternion start = PrimitivesConversionExtensions.PBQuaternionToUnityQuaternion(pbTween.Rotate.Start);
            Quaternion end = PrimitivesConversionExtensions.PBQuaternionToUnityQuaternion(pbTween.Rotate.End);
            startTransform.Rotation.Value = start;
            currentValue = start;
            return (start, end);
        }

        protected override TweenerCore<Quaternion, Quaternion, NoOptions> CreateTweener(Quaternion start, Quaternion end, float duration)
        {
            return DOTween.To(PureQuaternionPlugin.Plug(), () => currentValue,
                x => currentValue = x,
                end, duration);
        }

        public override void SetResult(ref SDKTransform sdkTransform)
        {
            sdkTransform.Rotation.Value = currentValue;
        }
    }
}
