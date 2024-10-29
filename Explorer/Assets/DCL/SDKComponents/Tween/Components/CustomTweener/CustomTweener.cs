using CrdtEcsBridge.Components.Transform;
using DCL.ECSComponents;
using DG.Tweening;
using DG.Tweening.Core;
using DG.Tweening.Plugins.Options;
using UnityEngine;

namespace DCL.SDKComponents.Tween.Components
{
    public abstract class CustomTweener<T, TU> : ICustomTweener
        where T : struct
        where TU : struct, IPlugOptions
    {
        private bool finished;
        protected T currentValue;
        private TweenerCore<T, T, TU> core;

        protected abstract TweenerCore<T, T, TU> CreateTweener(T start, T end, float duration);
        protected abstract (T, T) GetTweenValues(PBTween pbTween);
        public abstract void UpdateSDKTransform(ref SDKTransform sdkTransform);
        public abstract void UpdateTransform(Transform transform);

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

        public void Initialize(PBTween pbTween, float durationInSeconds)
        {
            core?.Kill();
            finished = false;

            var tweenValues = GetTweenValues(pbTween);
            core = CreateTweener(tweenValues.Item1, tweenValues.Item2, durationInSeconds);
        }
    }
}
