using CrdtEcsBridge.Components.Transform;
using DCL.ECSComponents;
using DG.Tweening;
using DG.Tweening.Core;
using DG.Tweening.Plugins.Options;
using System;
using UnityEngine;

namespace DCL.SDKComponents.Tween.Components
{
    public abstract class CustomTweener<T, TU> : ICustomTweener
        where T : struct
        where TU : struct, IPlugOptions
    {
        private bool Finished;
        protected T CurrentValue;
        protected TweenerCore<T, T, TU> core;
        protected abstract TweenerCore<T, T, TU> CreateTweener(T start, T end, float duration);
        protected abstract (T, T) GetTweenValues(PBTween pbTween, Transform startTransform);
        public abstract void SetResult(ref SDKTransform sdkTransform);

        public virtual void Clear()
        {
            core?.Kill();
        }

        public void Play()
        {
            core.Play();
        }

        public void Pause()
        {
            core.Pause();
        }

        public void Rewind()
        {
            core.Rewind();
        }

        public bool IsPaused()
        {
            return !core.IsPlaying();
        }

        public bool IsFinished()
        {
            return Finished;
        }

        public bool IsActive()
        {
            return !core.IsPlaying() && !Finished;
        }

        public void DoTween(Ease ease, float tweenModelCurrentTime, bool isPlaying)
        {
            core.SetEase(ease).SetAutoKill(false).OnComplete(() => { Finished = true; }).Goto(tweenModelCurrentTime, isPlaying);
        }

        public void Initialize(PBTween pbTween, Transform startTransform, float durationInSeconds)
        {
            core?.Kill();
            Finished = false;
            var tweenValues = GetTweenValues(pbTween, startTransform);
            core = CreateTweener(tweenValues.Item1, tweenValues.Item2, durationInSeconds);
        }
    }
}
