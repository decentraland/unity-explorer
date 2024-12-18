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
            return DOTween.To(() => CurrentValue, x => CurrentValue = x, end, duration);
        }

        protected override (Vector3, Vector3) GetTweenValues(PBTween pbTween)
        {
            Vector3 start = PrimitivesConversionExtensions.PBVectorToUnityVector(pbTween.Move.Start);
            Vector3 end = PrimitivesConversionExtensions.PBVectorToUnityVector(pbTween.Move.End);
            CurrentValue = start;
            return (start, end);
        }
    }

    public class ScaleTweener : CustomTweener<Vector3, VectorOptions>
    {
        protected override TweenerCore<Vector3, Vector3, VectorOptions> CreateTweener(Vector3 start, Vector3 end, float duration)
        {
            return DOTween.To(() => CurrentValue, x => CurrentValue = x, end, duration);
        }

        protected override (Vector3, Vector3) GetTweenValues(PBTween pbTween)
        {
            Vector3 start = PrimitivesConversionExtensions.PBVectorToUnityVector(pbTween.Scale.Start);
            Vector3 end = PrimitivesConversionExtensions.PBVectorToUnityVector(pbTween.Scale.End);
            CurrentValue = start;
            return (start, end);
        }
    }

    public class RotationTweener : CustomTweener<Quaternion, NoOptions>
    {
        protected override (Quaternion, Quaternion) GetTweenValues(PBTween pbTween)
        {
            Quaternion start = PrimitivesConversionExtensions.PBQuaternionToUnityQuaternion(pbTween.Rotate.Start);
            Quaternion end = PrimitivesConversionExtensions.PBQuaternionToUnityQuaternion(pbTween.Rotate.End);
            CurrentValue = start;
            return (start, end);
        }

        protected override TweenerCore<Quaternion, Quaternion, NoOptions> CreateTweener(Quaternion start, Quaternion end, float duration)
        {
            return DOTween.To(PureQuaternionPlugin.Plug(), () => CurrentValue,
                x => CurrentValue = x,
                end, duration);
        }
    }

    public class TextureMoveTweener : CustomTweener<Vector2, VectorOptions>
    {
        protected override TweenerCore<Vector2, Vector2, VectorOptions> CreateTweener(Vector2 start, Vector2 end, float duration) =>
            DOTween.To(() => CurrentValue, x => CurrentValue = x, end, duration);

        protected override (Vector2, Vector2) GetTweenValues(PBTween pbTween)
        {
            Vector2 start = pbTween.TextureMove.Start;
            Vector2 end = pbTween.TextureMove.End;
            CurrentValue = start;
            return (start, end);
        }
    }
}
