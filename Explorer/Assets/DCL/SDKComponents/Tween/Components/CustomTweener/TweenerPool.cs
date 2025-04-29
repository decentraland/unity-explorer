using CrdtEcsBridge.Components.Conversion;
using DCL.ECSComponents;
using System;
using UnityEngine.Pool;
using UnityEngine;

namespace DCL.SDKComponents.Tween.Components
{
    public class TweenerPool
    {
        private readonly IObjectPool<Vector3Tweener> vector3TweenerPool = new ObjectPool<Vector3Tweener>(() => new Vector3Tweener());
        private readonly IObjectPool<QuaternionTweener> quaternionTweenerPool = new ObjectPool<QuaternionTweener>(() => new QuaternionTweener());
        private readonly IObjectPool<Vector2Tweener> vector2TweenerPool = new ObjectPool<Vector2Tweener>(() => new Vector2Tweener());

        public ITweener GetTweener(PBTween pbTween, float durationInSeconds)
        {
            switch (pbTween.ModeCase)
            {
                case PBTween.ModeOneofCase.Move:
                    return GetVector3Tweener(pbTween.Move.Start, pbTween.Move.End, durationInSeconds);
                case PBTween.ModeOneofCase.Rotate:
                    // These conversions are needed because the Decentraland.Common.Quaternion type from the protobuf file
                    // is not directly compatible with the UnityEngine.Quaternion
                    Quaternion start = PrimitivesConversionExtensions.PBQuaternionToUnityQuaternion(pbTween.Rotate.Start);
                    Quaternion end = PrimitivesConversionExtensions.PBQuaternionToUnityQuaternion(pbTween.Rotate.End);
                    return GetQuaternionTweener(start, end, durationInSeconds);
                case PBTween.ModeOneofCase.Scale:
                    return GetVector3Tweener(pbTween.Scale.Start, pbTween.Scale.End, durationInSeconds);
                case PBTween.ModeOneofCase.TextureMove:
                    return GetVector2Tweener(pbTween.TextureMove.Start, pbTween.TextureMove.End, durationInSeconds);
                case PBTween.ModeOneofCase.None:
                default:
                    throw new ArgumentException($"No Tweener defined for tween mode: {pbTween.ModeCase}");
            }
        }

        private Vector3Tweener GetVector3Tweener(Vector3 start, Vector3 end, float durationInSeconds)
        {
            Vector3Tweener tweener = vector3TweenerPool.Get();
            tweener.Initialize(start, end, durationInSeconds);
            return tweener;
        }

        private QuaternionTweener GetQuaternionTweener(Quaternion start, Quaternion end, float durationInSeconds)
        {
            QuaternionTweener tweener = quaternionTweenerPool.Get();
            tweener.Initialize(start, end, durationInSeconds);
            return tweener;
        }

        private Vector2Tweener GetVector2Tweener(Vector2 start, Vector2 end, float durationInSeconds)
        {
            Vector2Tweener tweener = vector2TweenerPool.Get();
            tweener.Initialize(start, end, durationInSeconds);
            return tweener;
        }

        public void ReleaseCustomTweenerFrom(SDKTweenComponent sdkTweenComponent)
        {
            if (sdkTweenComponent.CustomTweener == null)
                return;

            switch (sdkTweenComponent.CustomTweener)
            {
                case Vector2Tweener vector2Tweener:
                    vector2TweenerPool.Release(vector2Tweener);
                    break;
                case Vector3Tweener vector3Tweener:
                    vector3TweenerPool.Release(vector3Tweener);
                    break;
                case QuaternionTweener quaternionTweener:
                    quaternionTweenerPool.Release(quaternionTweener);
                    break;
            }

            sdkTweenComponent.CustomTweener = null;
        }
    }
}
