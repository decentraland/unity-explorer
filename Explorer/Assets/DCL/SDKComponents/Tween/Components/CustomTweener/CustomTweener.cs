using DG.Tweening;
using DG.Tweening.Core;
using DG.Tweening.Plugins.Options;
using UnityEngine;

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
        private Ease ease;

        public T CurrentValue { get; set; }

        protected CustomTweener()
        {
            onCompleteCallback = OnTweenComplete;
        }

        public void Initialize(T startValue, T endValue, float durationInSeconds)
        {
            core?.Kill();
            finished = false;
            core = CreateTweener(startValue, endValue, durationInSeconds);
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
            Debug.Log($"VVV DoTween start current time: {tweenModelCurrentTime}");

            this.ease = ease;
            core.SetEase(ease).SetAutoKill(false).OnComplete(onCompleteCallback).Goto(tweenModelCurrentTime, isPlaying);
        }

        public Vector3? GetOffset(float delay)
        {
            if (core is not TweenerCore<Vector3,Vector3,VectorOptions> tw)
                return null;

            float t1 = tw.Elapsed(false);
            float t0 = Mathf.Clamp(t1 - delay, 0f, tw.Duration(false));

            return GetOffset(tw, t0, t1, ease);
        }

        public static Vector3 GetOffset(
            TweenerCore<Vector3,Vector3,VectorOptions> tw,
            float t0, float t1, Ease ease,
            float overshootOrAmplitude = 1.70158f, float period = 0f)
        {
            if (t1 < t0) (t0, t1) = (t1, t0);

            float dur = tw.Duration(false);
            Vector3 seg = tw.endValue - tw.startValue;

            float p0 = Mathf.Clamp01(t0 / dur);
            float p1 = Mathf.Clamp01(t1 / dur);

            // evaluate the same curve DOTween uses
            p0 = DOVirtual.EasedValue(0,1,p0,ease,overshootOrAmplitude,period);
            p1 = DOVirtual.EasedValue(0,1,p1,ease,overshootOrAmplitude,period);

            return seg * (p1 - p0);
        }

        private void OnTweenComplete()
        {
            finished = true;
        }
    }
}
