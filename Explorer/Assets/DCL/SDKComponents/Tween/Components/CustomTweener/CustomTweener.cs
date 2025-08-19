using DG.Tweening;
using DG.Tweening.Core;
using DG.Tweening.Plugins.Options;

namespace DCL.SDKComponents.Tween.Components
{
    public abstract class CustomTweener<T, TU> : ITweener
        where T: struct
        where TU: struct, IPlugOptions
    {
        private readonly TweenCallback onCompleteCallback;

        private bool finished;
        private TweenerCore<T, T, TU> core;
        private ITweener customTweenerImplementation;

        private readonly DOGetter<T> getValue;
        private readonly DOSetter<T> setValue;

        public T CurrentValue { get; private set; }

        protected CustomTweener()
        {
            onCompleteCallback = OnTweenComplete;

            getValue = GetCurrentValue;
            setValue = SetCurrentValue;
        }

        public void Initialize(T startValue, T endValue, float durationInSeconds)
        {
            core?.Kill();
            finished = false;
            core = CreateTweenerCore(startValue, endValue, durationInSeconds);
        }

        private TweenerCore<T, T, TU> CreateTweenerCore(T start, T end, float duration)
        {
            SetCurrentValue(start);
            return CreateTweener(getValue, setValue, end, duration);
        }

        protected abstract TweenerCore<T, T, TU> CreateTweener(DOGetter<T> getter, DOSetter<T> setter, T end, float duration);

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
            core.SetEase(ease).SetAutoKill(false).OnComplete(onCompleteCallback).Goto(tweenModelCurrentTime, isPlaying);
        }

        private void OnTweenComplete()
        {
            finished = true;
        }

        protected void SetCurrentValue(T value)
        {
            CurrentValue = value;
        }

        protected T GetCurrentValue() =>
            CurrentValue;
    }
}
