using CrdtEcsBridge.Components.Conversion;
using DCL.ECSComponents;
using UnityEngine.Pool;
using UnityEngine;

namespace DCL.SDKComponents.Tween.Components
{
    public class TweenerPool
    {
        private readonly IObjectPool<Vector3Tweener> vector3TweenerPool;
        private readonly IObjectPool<QuaternionTweener> quaternionTweenerPool;
        private readonly IObjectPool<Vector2Tweener> vector2TweenerPool;

        public TweenerPool()
        {
            vector3TweenerPool = new ObjectPool<Vector3Tweener>(() => new Vector3Tweener());
            quaternionTweenerPool = new ObjectPool<QuaternionTweener>(() => new QuaternionTweener());
            vector2TweenerPool = new ObjectPool<Vector2Tweener>(() => new Vector2Tweener());
        }

        public ITweener GetTweener(PBTween pbTween, float durationInSeconds)
        {
            ITweener tweener = null;

            switch (pbTween.ModeCase)
            {
                case PBTween.ModeOneofCase.Move:
                    tweener = vector3TweenerPool.Get();
                    ((ICustomTweener<Vector3>)tweener).Initialize(pbTween.Move.Start, pbTween.Move.End, durationInSeconds);
                    break;
                case PBTween.ModeOneofCase.Rotate:
                    tweener = quaternionTweenerPool.Get();

                    // These conversions are needed because the Decentraland.Common.Quaternion type from the protobuf file
                    // is not directly compatible with the UnityEngine.Quaternion
                    Quaternion start = PrimitivesConversionExtensions.PBQuaternionToUnityQuaternion(pbTween.Rotate.Start);
                    Quaternion end = PrimitivesConversionExtensions.PBQuaternionToUnityQuaternion(pbTween.Rotate.End);
                    ((ICustomTweener<Quaternion>)tweener).Initialize(start, end, durationInSeconds);
                    break;
                case PBTween.ModeOneofCase.Scale:
                    tweener = vector3TweenerPool.Get();
                    ((ICustomTweener<Vector3>)tweener).Initialize(pbTween.Scale.Start, pbTween.Scale.End, durationInSeconds);
                    break;
                case PBTween.ModeOneofCase.TextureMove:
                    tweener = vector2TweenerPool.Get();
                    ((ICustomTweener<Vector2>)tweener).Initialize(pbTween.TextureMove.Start, pbTween.TextureMove.End, durationInSeconds);
                    break;
            }

            return tweener;
        }

        public void Return(SDKTweenComponent sdkTweenComponent)
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
        }
    }
}
