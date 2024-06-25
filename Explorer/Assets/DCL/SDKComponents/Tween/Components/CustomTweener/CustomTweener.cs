using CRDT;
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
        protected TweenerCore<T, T, TU> core;
        protected T CurrentValue { get;  set; }

        protected Transform StartTransform;
        private bool Finished;

        public void Play()
        {
            core.Play();
        }

        public void Pause()
        {
            core.Pause();
        }

        public void Kill()
        {
            core.Kill();
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

        protected abstract TweenerCore<T, T, TU> CreateTweener(T start, T end, float duration);

        public abstract TweenResult GetResult();
        public CRDTEntity ParentId { get; set; }
    }
}