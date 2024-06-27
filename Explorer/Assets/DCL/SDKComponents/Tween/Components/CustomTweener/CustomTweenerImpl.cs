using CommunityToolkit.HighPerformance.Helpers;
using CrdtEcsBridge.Components.Conversion;
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

        public override void Initialize(PBTween pbTween, Transform startTransform, float durationInSeconds)
        {
            var start = PrimitivesConversionExtensions.PBVectorToUnityVector(pbTween.Move.Start);
            var end = PrimitivesConversionExtensions.PBVectorToUnityVector(pbTween.Move.End);
            startTransform.localPosition = start;

            InternalInit(startTransform, start, end, durationInSeconds);
        }

        protected override TweenerCore<Vector3, Vector3, VectorOptions> CreateTweener(Vector3 start, Vector3 end, float duration)
        {
            return DOTween.To(() => CurrentValue.Position,
                x => WriteResult(x, CurrentValue.Rotation, CurrentValue.Scale),
                end, duration);
        }
    }

    public class ScaleTweener : CustomTweener<Vector3, VectorOptions>
    {

        public override void Initialize(PBTween pbTween, Transform startTransform, float durationInSeconds)
        {
            var start = PrimitivesConversionExtensions.PBVectorToUnityVector(pbTween.Scale.Start);
            var end = PrimitivesConversionExtensions.PBVectorToUnityVector(pbTween.Scale.End);
            startTransform.localScale = start;

            InternalInit(startTransform, start, end, durationInSeconds);
        }

        protected override TweenerCore<Vector3, Vector3, VectorOptions> CreateTweener(Vector3 start, Vector3 end, float duration)
        {
            return DOTween.To(() => CurrentValue.Scale,
                x => WriteResult(CurrentValue.Position, CurrentValue.Rotation, x),
                end, duration);
        }
    }

    public class RotationTweener : CustomTweener<Quaternion, NoOptions>
    {
        protected sealed override TweenerCore<Quaternion, Quaternion, NoOptions> CreateTweener(Quaternion start, Quaternion end, float duration)
        {
            return DOTween.To(PureQuaternionPlugin.Plug(), () => CurrentValue.Rotation,
                x => WriteResult(CurrentValue.Position, x, CurrentValue.Scale),
                end, duration);
        }


        public override void Initialize(PBTween pbTween, Transform startTransform, float durationInSeconds)
        {
            var start = PrimitivesConversionExtensions.PBQuaternionToUnityQuaternion(pbTween.Rotate.Start);
            var end = PrimitivesConversionExtensions.PBQuaternionToUnityQuaternion(pbTween.Rotate.End);
            startTransform.localRotation = start;

            InternalInit(startTransform, start, end, durationInSeconds);
        }
    }
}