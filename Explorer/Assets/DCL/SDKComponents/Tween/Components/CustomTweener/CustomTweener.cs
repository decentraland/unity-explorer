using DCL.ECSComponents;
using DG.Tweening;
using DG.Tweening.Core;
using DG.Tweening.Plugins.Options;

namespace DCL.SDKComponents.Tween.Components
{
    public abstract class CustomTweener<T, TU> : ICustomTweener<T>
        where T: struct
        where TU: struct, IPlugOptions
    {
        private T currentValue;
        private bool finished;
        private TweenerCore<T, T, TU> core;
        private ICustomTweener<T> customTweenerImplementation;

        public T CurrentValue
        {
            get => currentValue;

            set => currentValue = value;
        }

        public void Initialize(PBTween pbTween, float durationInSeconds)
        {
            core?.Kill();
            finished = false;

            (T, T) tweenValues = GetTweenValues(pbTween);
            core = CreateTweener(tweenValues.Item1, tweenValues.Item2, durationInSeconds);
        }

        protected abstract TweenerCore<T, T, TU> CreateTweener(T start, T end, float duration);

        protected abstract (T, T) GetTweenValues(PBTween pbTween);

        public void Play() =>
            core.Play();

        public void Pause() =>
            core.Pause();

        public void Rewind() =>
            core.Rewind();

        public bool IsPaused() =>
            !core.IsPlaying();

        public bool IsFinished() =>
            finished;

        public bool IsActive() =>
            !core.IsPlaying() && !finished;

        public void DoTween(Ease ease, float tweenModelCurrentTime, bool isPlaying)
        {
            core.SetEase(ease).SetAutoKill(false).OnComplete(() => { finished = true; }).Goto(tweenModelCurrentTime, isPlaying);
        }
    }
}
