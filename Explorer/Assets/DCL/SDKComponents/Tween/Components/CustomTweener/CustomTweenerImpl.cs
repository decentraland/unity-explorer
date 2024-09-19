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

        protected override (Vector3, Vector3) GetTweenValues(PBTween pbTween, Transform startTransform)
        {
            Vector3 start = PrimitivesConversionExtensions.PBVectorToUnityVector(pbTween.Move.Start);
            Vector3 end = PrimitivesConversionExtensions.PBVectorToUnityVector(pbTween.Move.End);
            startTransform.localPosition = start;
            currentValue = start;
            return (start, end);
        }

        public override void SetResult(ref SDKTransform sdkTransform)
        {
            sdkTransform.Position.Value = currentValue;

            sdkTransform.Rotation.Value = startRotation;
            sdkTransform.Scale = startScale;
        }
    }

    public class ScaleTweener : CustomTweener<Vector3, VectorOptions>
    {
        protected override TweenerCore<Vector3, Vector3, VectorOptions> CreateTweener(Vector3 start, Vector3 end, float duration)
        {
            return DOTween.To(() => currentValue, x => currentValue = x, end, duration);
        }

        protected override (Vector3, Vector3) GetTweenValues(PBTween pbTween, Transform startTransform)
        {
            Vector3 start = PrimitivesConversionExtensions.PBVectorToUnityVector(pbTween.Scale.Start);
            Vector3 end = PrimitivesConversionExtensions.PBVectorToUnityVector(pbTween.Scale.End);
            startTransform.localScale = start;
            currentValue = start;
            return (start, end);
        }

        public override void SetResult(ref SDKTransform sdkTransform)
        {
            sdkTransform.Scale = currentValue;

            sdkTransform.Position.Value = startPosition;
            sdkTransform.Rotation.Value = startRotation;
        }
    }

    public class RotationTweener : CustomTweener<Quaternion, NoOptions>
    {
        protected override (Quaternion, Quaternion) GetTweenValues(PBTween pbTween, Transform startTransform)
        {
            Quaternion start = PrimitivesConversionExtensions.PBQuaternionToUnityQuaternion(pbTween.Rotate.Start);
            Quaternion end = PrimitivesConversionExtensions.PBQuaternionToUnityQuaternion(pbTween.Rotate.End);
            startTransform.localRotation = start;
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

            sdkTransform.Position.Value = startPosition;
            sdkTransform.Scale = startScale;
        }
    }
}
