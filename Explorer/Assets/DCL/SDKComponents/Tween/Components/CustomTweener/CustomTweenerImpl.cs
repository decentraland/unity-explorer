using CrdtEcsBridge.Components.Conversion;
using DCL.ECSComponents;
using DG.Tweening;
using DG.Tweening.Core;
using DG.Tweening.CustomPlugins;
using DG.Tweening.Plugins.Options;
using UnityEngine;

namespace DCL.SDKComponents.Tween.Components
{
    public abstract class Vector3CustomTweener : CustomTweener<Vector3, VectorOptions>
    {

        protected sealed override TweenerCore<Vector3, Vector3, VectorOptions> CreateTweener(Vector3 start, Vector3 end, float duration)
        {
            CurrentValue = start;
            return DOTween.To(() => CurrentValue, x => CurrentValue = x, end, duration);
        }

        public abstract override TweenResult GetResult();
    }

    public class PositionTweener : Vector3CustomTweener
    {
        public override TweenResult GetResult()
        {
            return new TweenResult
            {
                Position = CurrentValue, Rotation = StartTransform.localRotation, Scale = StartTransform.localScale
            };
        }

        public override void Initialize(PBTween pbTween, Transform startTransform, float durationInSeconds)
        {
            Dispose();
            StartTransform = startTransform;
            var start = PrimitivesConversionExtensions.PBVectorToUnityVector(pbTween.Move.Start);
            var end = PrimitivesConversionExtensions.PBVectorToUnityVector(pbTween.Move.End);
            core = CreateTweener(start, end, durationInSeconds);
            startTransform.localPosition = start;
        }
    }

    public class ScaleTweener : Vector3CustomTweener
    {
        public override TweenResult GetResult()
        {
            return new TweenResult
            {
                Position = StartTransform.localPosition, Rotation = StartTransform.localRotation, Scale = CurrentValue
            };
        }

        public override void Initialize(PBTween pbTween, Transform startTransform, float durationInSeconds)
        {
            Dispose();
            StartTransform = startTransform;
            var start = PrimitivesConversionExtensions.PBVectorToUnityVector(pbTween.Scale.Start);
            var end = PrimitivesConversionExtensions.PBVectorToUnityVector(pbTween.Scale.End);
            core = CreateTweener(start, end, durationInSeconds);
            startTransform.localScale = start;
        }
    }

    public class RotationTweener : CustomTweener<Quaternion, NoOptions>
    {
        protected sealed override TweenerCore<Quaternion, Quaternion, NoOptions> CreateTweener(Quaternion start, Quaternion end, float duration)
        {
            CurrentValue = start;
            return DOTween.To(PureQuaternionPlugin.Plug(), () => CurrentValue, x => CurrentValue = x, end, duration);
        }

        public override TweenResult GetResult()
        {
            return new TweenResult
            {
                Position = StartTransform.localPosition, Rotation = CurrentValue, Scale = StartTransform.localScale
            };
        }

        public override void Initialize(PBTween pbTween, Transform startTransform, float durationInSeconds)
        {
            Dispose();
            StartTransform = startTransform;
            var start = PrimitivesConversionExtensions.PBQuaternionToUnityQuaternion(pbTween.Rotate.Start);
            var end = PrimitivesConversionExtensions.PBQuaternionToUnityQuaternion(pbTween.Rotate.End);
            core = CreateTweener(start, end, durationInSeconds);
            startTransform.localRotation = start;
        }
    }
}