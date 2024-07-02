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

        protected abstract (T, T) GetTweenValues(PBTween pbTween, Transform startTransform);
        
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

        public TweenResult GetResult()
        {
            return CurrentValue;
        }

        public void Initialize(PBTween pbTween, Transform startTransform, float durationInSeconds)
        {
            core?.Kill();
            Finished = false;
            CurrentValue = new TweenResult
            {
                Position = startTransform.localPosition, Rotation = startTransform.localRotation, Scale = startTransform.localScale
            };
            var tweenValues = GetTweenValues(pbTween, startTransform);
            core = CreateTweener(tweenValues.Item1, tweenValues.Item2, durationInSeconds);
        }

        protected void WriteResult(Vector3 position, Quaternion rotation, Vector3 scale)
        {
            CurrentValue.Position = position;
            CurrentValue.Rotation = rotation;
            CurrentValue.Scale = scale;
        }


    }
}