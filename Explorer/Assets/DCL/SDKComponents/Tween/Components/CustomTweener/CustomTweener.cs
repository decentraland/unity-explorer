using DG.Tweening;
using DG.Tweening.Core;
using DG.Tweening.Plugins.Options;

namespace DCL.SDKComponents.Tween.Components
{
    public abstract class CustomTweener<T, TU> : ICustomTweener<T>
        where T: struct
        where TU: struct, IPlugOptions
    {
        private bool finished;
        private TweenerCore<T, T, TU> core;
        private ICustomTweener<T> customTweenerImplementation;

        public T CurrentValue { get; set; }

        public void Initialize(T startValue, T endValue, float durationInSeconds)
        {
            core?.Kill();
            finished = false;
            core = CreateTweener(startValue, endValue, durationInSeconds);
            core.OnComplete(OnTweenComplete);
        }

        protected abstract TweenerCore<T, T, TU> CreateTweener(T start, T end, float duration);

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
            core.SetEase(ease).SetAutoKill(false).Goto(tweenModelCurrentTime, isPlaying);
        }
        
        private void OnTweenComplete()
        {
            finished = true;
        }
    }
}
