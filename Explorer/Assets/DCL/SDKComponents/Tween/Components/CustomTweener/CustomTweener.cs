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

        public ulong StartSyncServerTimeMs { private get; set; }

        public void DoTween(Ease ease, float tweenModelCurrentTime, bool isPlaying)
        {
            Debug.Log($"VVV DoTween start current time: {tweenModelCurrentTime}");

            this.ease = ease;
            core.SetEase(ease).SetAutoKill(false).OnComplete(onCompleteCallback).Goto(tweenModelCurrentTime, isPlaying);
        }

        // public Vector3? GetOffset(float tPast, float tCurrent)
        public Vector3? GetOffset(ulong syncTimePast, ulong syncTimeServer)
        {
            if (this is not Vector3Tweener vector3Tweener) return null;
            if (core is not TweenerCore<Vector3,Vector3,VectorOptions> tw) return null;

            var current = vector3Tweener.CurrentValue;

            var pastTime = Mathf.Clamp((syncTimeServer - syncTimePast) / 1000f, 0f, tw.Duration(false));
            core.Goto(pastTime);
            var past = vector3Tweener.CurrentValue;

            var currentTime = Mathf.Clamp((syncTimeServer - StartSyncServerTimeMs) / 1000f, 0f, tw.Duration(false));
            core.Goto(currentTime);

            Debug.Log($"VVV [TWEEN] {current} {past}");

            return current - past;

            // float t1 = tw.Elapsed(false);
            // float t0 = Mathf.Clamp(t1 - delay, 0f, tw.Duration(false));
            // if (core is not TweenerCore<Vector3,Vector3,VectorOptions> tw) return null;
            return GetOffset(tw, syncTimeServer - syncTimePast, syncTimeServer - StartSyncServerTimeMs, ease);
        }

        public static Vector3 GetOffset(
            TweenerCore<Vector3,Vector3,VectorOptions> tw,
            ulong tPast, ulong tCurrent, Ease ease,
            float overshootOrAmplitude = 1.70158f, float period = 0f)
        {
            // if (tCurrent < tPast) (tPast, tCurrent) = (tCurrent, tPast);

            Vector3 seg = tw.endValue - tw.startValue;

            float dur = tw.Duration(false);
            float pPast = Mathf.Clamp01(tPast / dur);
            float pCurrent = Mathf.Clamp01(tCurrent / dur);

            // evaluate the same curve DOTween uses
            pPast = DOVirtual.EasedValue(0,1,pPast,ease,overshootOrAmplitude,period);
            pCurrent = DOVirtual.EasedValue(0,1,pCurrent,ease,overshootOrAmplitude,period);

            return seg * (pCurrent - pPast);
        }

        private void OnTweenComplete()
        {
            finished = true;
        }
    }
}
