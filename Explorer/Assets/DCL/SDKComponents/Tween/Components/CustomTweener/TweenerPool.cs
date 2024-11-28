using DCL.ECSComponents;
using UnityEngine.Pool;

namespace DCL.SDKComponents.Tween.Components
{
    public class TweenerPool
    {
        private readonly IObjectPool<RotationTweener> rotationTweenersPool;
        private readonly IObjectPool<PositionTweener> positionTweenersPool;
        private readonly IObjectPool<ScaleTweener> scaleTweenersPool;
        private readonly IObjectPool<TextureMoveTweener> textureMoveTweenersPool;

        public TweenerPool()
        {
            rotationTweenersPool = new ObjectPool<RotationTweener>(() => new RotationTweener());
            positionTweenersPool = new ObjectPool<PositionTweener>(() => new PositionTweener());
            scaleTweenersPool = new ObjectPool<ScaleTweener>(() => new ScaleTweener());
            textureMoveTweenersPool = new ObjectPool<TextureMoveTweener>(() => new TextureMoveTweener());
        }

        public ICustomTweener<T> GetTweener<T>(PBTween pbTween, float durationInSeconds)
        {
            ICustomTweener<T> tweener = null;
            switch (pbTween.ModeCase)
            {
                case PBTween.ModeOneofCase.Move:
                    tweener = (ICustomTweener<T>)positionTweenersPool.Get();
                    break;
                case PBTween.ModeOneofCase.Rotate:
                    tweener = (ICustomTweener<T>)rotationTweenersPool.Get();
                    break;
                case PBTween.ModeOneofCase.Scale:
                    tweener = (ICustomTweener<T>)scaleTweenersPool.Get();
                    break;
                case PBTween.ModeOneofCase.TextureMove:
                    tweener = (ICustomTweener<T>)textureMoveTweenersPool.Get();
                    break;
            }

            tweener!.Initialize(pbTween, durationInSeconds);
            return tweener;
        }

        public void Return<T>(SDKTweenComponent<T> sdkTweenComponent)
        {
            if (sdkTweenComponent.CustomTweener == null)
                return;

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
                case TextureMoveTweener textureMoveTweener:
                    textureMoveTweenersPool.Release(textureMoveTweener);
                    break;
            }
        }
    }
}
