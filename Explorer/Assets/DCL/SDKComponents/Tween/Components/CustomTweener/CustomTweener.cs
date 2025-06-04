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
            // Debug.Log($"VVV DoTween start current time: {tweenModelCurrentTime}");

            this.ease = ease;
            core.SetEase(ease).SetAutoKill(false).OnComplete(onCompleteCallback).Goto(tweenModelCurrentTime, isPlaying);
        }

        public Vector3? GetFuture(float futureDeltaTime)
        {
            if (this is not Vector3Tweener vector3Tweener) return null;
            if (core is not TweenerCore<Vector3,Vector3,VectorOptions> tw) return null;

            float currentTweenTime = tw.Elapsed(false);
            float dur = tw.Duration(false);
            Vector3 future = DOVirtual.EasedValue(tw.startValue, tw.endValue, Mathf.Clamp01((currentTweenTime + futureDeltaTime ) / dur), ease, 1.70158f,0f);

            return future - vector3Tweener.CurrentValue;
        }

        public Vector3? GetOffset(float deltaTime, ulong syncTimePast, ulong syncTimeServer)
        {
            if (this is not Vector3Tweener vector3Tweener) return null;
            if (core is not TweenerCore<Vector3,Vector3,VectorOptions> tw) return null;

            float currentTweenTime = tw.Elapsed(false);
            float dur = tw.Duration(false);

            if (syncTimeServer < syncTimePast)
                Debug.LogError("VVV [TWEEN] Server time is less then Past message time");

            float timeDiff = (syncTimeServer - syncTimePast) / 1000f;
            float pastTweenTime = Mathf.Clamp(currentTweenTime - timeDiff, 0f, dur);

            Vector3 past = DOVirtual.EasedValue(tw.startValue, tw.endValue, Mathf.Clamp01(pastTweenTime / dur), ease, 1.70158f,0f);
            Vector3 future = DOVirtual.EasedValue(tw.startValue, tw.endValue, Mathf.Clamp01((currentTweenTime + deltaTime ) / dur), ease, 1.70158f,0f);

            // return vector3Tweener.CurrentValue - past;
            // return vector3Tweener.CurrentValue;
            return future - past;
        }

        private void OnTweenComplete()
        {
            finished = true;
        }
    }
}
