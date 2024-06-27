using CRDT;
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
        private bool Finished;
        protected TweenResult CurrentValue;

        protected TweenerCore<T, T, TU> core;
        protected abstract TweenerCore<T, T, TU> CreateTweener(T start, T end, float duration);

        public abstract void Initialize(PBTween pbTween, Transform startTransform, float durationInSeconds);

        public CRDTEntity ParentId { get; set; }
        
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

        public void Dispose()
        {
            core?.Kill();
        }

        public TweenResult GetResult()
        {
            return CurrentValue;
        }

        protected void InternalInit(Transform transform, T start, T end, float durationInSeconds)
        {
            core?.Kill();
            CurrentValue = new TweenResult
            {
                Position = transform.localPosition, Rotation = transform.localRotation, Scale = transform.localScale
            };
            core = CreateTweener(start, end, durationInSeconds);
        }

        protected void WriteResult(Vector3 position, Quaternion rotation, Vector3 scale)
        {
            CurrentValue.Position = position;
            CurrentValue.Rotation = rotation;
            CurrentValue.Scale = CurrentValue.Scale;
        }


    }
}