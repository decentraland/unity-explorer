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
            rotationTweenersPool = new ObjectPool<RotationTweener>(() => new RotationTweener(), actionOnRelease: tweener => tweener.Dispose());
            positionTweenersPool = new ObjectPool<PositionTweener>(() => new PositionTweener(), actionOnRelease: tweener => tweener.Dispose());
            scaleTweenersPool = new ObjectPool<ScaleTweener>(() => new ScaleTweener(), actionOnRelease: tweener => tweener.Dispose());
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

            if (sdkTweenComponent.CustomTweener is PositionTweener)
                positionTweenersPool.Release((PositionTweener)sdkTweenComponent.CustomTweener);
            else if (sdkTweenComponent.CustomTweener is RotationTweener)
                rotationTweenersPool.Release((RotationTweener)sdkTweenComponent.CustomTweener);
            else if (sdkTweenComponent.CustomTweener is ScaleTweener)
                scaleTweenersPool.Release((ScaleTweener)sdkTweenComponent.CustomTweener);
        }
    }
}