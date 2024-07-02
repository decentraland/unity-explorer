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
        
        protected override TweenerCore<Vector3, Vector3, VectorOptions> CreateTweener(Vector3 start, Vector3 end, float duration)
        {
            return DOTween.To(() => CurrentValue.Position,
                x => WriteResult(x, CurrentValue.Rotation, CurrentValue.Scale),
                end, duration);
        }

        protected override (Vector3, Vector3) GetTweenValues(PBTween pbTween, Transform startTransform)
        {
            var start = PrimitivesConversionExtensions.PBVectorToUnityVector(pbTween.Move.Start);
            var end = PrimitivesConversionExtensions.PBVectorToUnityVector(pbTween.Move.End);
            startTransform.localPosition = start;
            return (start, end);
        }
    }

    public class ScaleTweener : CustomTweener<Vector3, VectorOptions>
    {
        protected override (Vector3, Vector3) GetTweenValues(PBTween pbTween, Transform startTransform)
        {
            var start = PrimitivesConversionExtensions.PBVectorToUnityVector(pbTween.Scale.Start);
            var end = PrimitivesConversionExtensions.PBVectorToUnityVector(pbTween.Scale.End);
            startTransform.localScale = start;
            return (start, end);
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
        protected override TweenerCore<Quaternion, Quaternion, NoOptions> CreateTweener(Quaternion start, Quaternion end, float duration)
        {
            return DOTween.To(PureQuaternionPlugin.Plug(), () => CurrentValue.Rotation,
                x => WriteResult(CurrentValue.Position, x, CurrentValue.Scale),
                end, duration);
        }

        protected override (Quaternion, Quaternion) GetTweenValues(PBTween pbTween, Transform startTransform)
        {
            var start = PrimitivesConversionExtensions.PBQuaternionToUnityQuaternion(pbTween.Rotate.Start);
            var end = PrimitivesConversionExtensions.PBQuaternionToUnityQuaternion(pbTween.Rotate.End);
            startTransform.localRotation = start;
            return (start, end);
        }

    }
}