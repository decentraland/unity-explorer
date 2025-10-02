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

        public ITweener GetTweener(PBTween pbTween, float durationInSeconds, Transform? transform = null)
        {
            switch (pbTween.ModeCase)
            {
                case PBTween.ModeOneofCase.Move:
                    return GetVector3Tweener(pbTween.Move.Start, pbTween.Move.End, durationInSeconds);
                case PBTween.ModeOneofCase.Rotate:
                    return GetQuaternionTweener(pbTween.Rotate.Start.ToUnityQuaternion(), pbTween.Rotate.End.ToUnityQuaternion(), durationInSeconds);
                case PBTween.ModeOneofCase.Scale:
                    return GetVector3Tweener(pbTween.Scale.Start, pbTween.Scale.End, durationInSeconds);
                case PBTween.ModeOneofCase.TextureMove:
                    return GetVector2Tweener(pbTween.TextureMove.Start, pbTween.TextureMove.End, durationInSeconds);
                case PBTween.ModeOneofCase.RotateContinuous:
                    return GetContinuousQuaternionTweener(transform ? transform.localRotation : Quaternion.identity, pbTween.RotateContinuous.Direction.ToUnityQuaternion(), pbTween.RotateContinuous.Speed);
                /*case PBTween.ModeOneofCase.MoveContinuous:
                    return GetContinuousVector3Tweener(pbTween.MoveContinuous.Direction, pbTween.MoveContinuous.Speed, durationInSeconds);
                case PBTween.ModeOneofCase.TextureMoveContinuous:
                    return GetContinuousVector2Tweener(pbTween.TextureMoveContinuous.Direction, pbTween.TextureMoveContinuous.Speed, durationInSeconds);*/
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

        private QuaternionTweener GetContinuousQuaternionTweener(Quaternion start, Quaternion direction, float speed)
        {
            QuaternionTweener tweener = quaternionTweenerPool.Get();
            tweener.InitializeContinuous(start, direction, speed);
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
