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
        private Vector3 startScale;
        private Quaternion startRotation;

        protected override TweenerCore<Vector3, Vector3, VectorOptions> CreateTweener(Vector3 start, Vector3 end, float duration)
        {
            return DOTween.To(() => CurrentValue, x => CurrentValue = x, end, duration);
        }

        protected override (Vector3, Vector3) GetTweenValues(PBTween pbTween, SDKTransform sdkTransform, Transform startTransform)
        {
            startScale = startTransform.localScale;
            startRotation = startTransform.localRotation;

            Vector3 start = PrimitivesConversionExtensions.PBVectorToUnityVector(pbTween.Move.Start);
            Vector3 end = PrimitivesConversionExtensions.PBVectorToUnityVector(pbTween.Move.End);
            startTransform.localPosition = start;

            CurrentValue = start;
            return (start, end);
        }

        public override void SetResult(ref SDKTransform sdkTransform)
        {
            sdkTransform.Position = CurrentValue;

            sdkTransform.Rotation = startRotation;
            sdkTransform.Scale = startScale;
        }
    }

    public class ScaleTweener : CustomTweener<Vector3, VectorOptions>
    {
        private Vector3 startPosition;
        private Quaternion startRotation;

        protected override TweenerCore<Vector3, Vector3, VectorOptions> CreateTweener(Vector3 start, Vector3 end, float duration)
        {
            return DOTween.To(() => CurrentValue, x => CurrentValue = x, end, duration);
        }

        protected override (Vector3, Vector3) GetTweenValues(PBTween pbTween, SDKTransform sdkTransform, Transform startTransform)
        {
            startPosition = startTransform.localPosition;
            startRotation = startTransform.localRotation;

            Vector3 start = PrimitivesConversionExtensions.PBVectorToUnityVector(pbTween.Scale.Start);
            Vector3 end = PrimitivesConversionExtensions.PBVectorToUnityVector(pbTween.Scale.End);
            startTransform.localScale = start;
            CurrentValue = start;
            return (start, end);
        }

        public override void SetResult(ref SDKTransform sdkTransform)
        {
            sdkTransform.Scale = CurrentValue;

            sdkTransform.Position = startPosition;
            sdkTransform.Rotation = startRotation;
        }
    }

    public class RotationTweener : CustomTweener<Quaternion, NoOptions>
    {
        private Vector3 startPosition;
        private Vector3 startScale;

        protected override (Quaternion, Quaternion) GetTweenValues(PBTween pbTween, SDKTransform sdkTransform, Transform startTransform)
        {
            Quaternion start = PrimitivesConversionExtensions.PBQuaternionToUnityQuaternion(pbTween.Rotate.Start);
            Quaternion end = PrimitivesConversionExtensions.PBQuaternionToUnityQuaternion(pbTween.Rotate.End);
            startTransform.localRotation = start;

            startPosition = startTransform.localPosition;
            startScale = startTransform.localScale;

            CurrentValue = start;
            return (start, end);
        }

        protected override TweenerCore<Quaternion, Quaternion, NoOptions> CreateTweener(Quaternion start, Quaternion end, float duration)
        {
            return DOTween.To(PureQuaternionPlugin.Plug(), () => CurrentValue,
                x => CurrentValue = x,
                end, duration);
        }

        public override void SetResult(ref SDKTransform sdkTransform)
        {
            sdkTransform.Rotation = CurrentValue;

            sdkTransform.Position = startPosition;
            sdkTransform.Scale = startScale;
        }
    }
}
