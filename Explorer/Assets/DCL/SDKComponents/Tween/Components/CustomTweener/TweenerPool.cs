using DCL.ECSComponents;
using UnityEngine;
using UnityEngine.Pool;

namespace DCL.SDKComponents.Tween.Components
{
    public class TweenerPool
    {
        private readonly IObjectPool<RotationTweener> rotationTweenersPool;
        private readonly IObjectPool<PositionTweener> positionTweenersPool;
        private readonly IObjectPool<ScaleTweener> scaleTweenersPool;

        public TweenerPool()
        {
            rotationTweenersPool = new ObjectPool<RotationTweener>(() => new RotationTweener());
            positionTweenersPool = new ObjectPool<PositionTweener>(() => new PositionTweener());
            scaleTweenersPool = new ObjectPool<ScaleTweener>(() => new ScaleTweener());
        }

        public ICustomTweener GetTweener(PBTween pbTween, Transform entityTransform, float durationInSeconds)
        {
            ICustomTweener tweener = null;
            switch (pbTween.ModeCase)
            {
                case PBTween.ModeOneofCase.Move:
                    tweener = positionTweenersPool.Get();
                    break;
                case PBTween.ModeOneofCase.Rotate:
                    tweener = rotationTweenersPool.Get();
                    break;
                case PBTween.ModeOneofCase.Scale:
                    tweener = scaleTweenersPool.Get();
                    break;
            }

            tweener!.Initialize(pbTween, entityTransform, durationInSeconds);
            return tweener;
        }

        public void Return(SDKTweenComponent sdkTweenComponent)
        {
            if (sdkTweenComponent.CustomTweener == null)
                return;

            sdkTweenComponent.CustomTweener.Clear();

            switch (sdkTweenComponent.CustomTweener)
            {
                case PositionTweener positionTweener:
                    positionTweenersPool.Release(positionTweener);
                    break;
                case RotationTweener rotationTweener:
                    rotationTweenersPool.Release(rotationTweener);
                    break;
                case ScaleTweener scaleTweener:
                    scaleTweenersPool.Release(scaleTweener);
                    break;
            }
        }
    }
}
