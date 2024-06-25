using CRDT;
using CrdtEcsBridge.Components.Transform;
using DCL.ECSComponents;
using DG.Tweening;
using DG.Tweening.Core;
using DG.Tweening.CustomPlugins;
using DG.Tweening.Plugins.Core;
using DG.Tweening.Plugins.Options;
using ECS.Unity.Transforms.Components;
using UnityEngine;

namespace DCL.SDKComponents.Tween.Components
{
    public interface ICustomTweener
    {
        float ElapsedPercentage();
        public (Vector3, Quaternion, Vector3) GetResult();
        public CRDTEntity ParentId { get; set; }
        void DoTween(Ease ease, float tweenModelCurrentTime, bool isPlaying);
        bool Finished { get; }
    }

    public abstract class CustomTweener<T, TU> : ICustomTweener
        where T : struct
        where TU : struct, IPlugOptions
    {
        protected TweenerCore<T, T, TU> core;
        protected T CurrentValue { get;  set; }

        protected Transform StartTransform;
        public bool Finished { get; private set;  }

        public void DoTween(Ease ease, float tweenModelCurrentTime, bool isPlaying)
        {
            core.SetEase(ease).SetAutoKill(false).OnComplete(() => { Finished = true; });
            core.Goto(tweenModelCurrentTime, isPlaying);
        }


        public float ElapsedPercentage()
        {
            if (core != null)
                return core.ElapsedPercentage();
            return 0;
        }

        protected abstract TweenerCore<T, T, TU> CreateTweener(T start, T end, float duration);

        public abstract (Vector3, Quaternion, Vector3) GetResult();
        public CRDTEntity ParentId { get; set; }
    }

    public abstract class Vector3CustomTweener : CustomTweener<Vector3, VectorOptions>
    {
        public Vector3CustomTweener(Transform startTransform, Vector3 start, Vector3 end, float duration)
        {
            StartTransform = startTransform;
            core = CreateTweener(start, end, duration);
        }

        protected sealed override TweenerCore<Vector3, Vector3, VectorOptions> CreateTweener(Vector3 start, Vector3 end, float duration)
        {
            CurrentValue = start;
            return DOTween.To(() => CurrentValue, x => CurrentValue = x, end, duration);
        }

        public abstract override (Vector3, Quaternion, Vector3) GetResult();
    }

    public class PositionTweener : Vector3CustomTweener
    {
        public PositionTweener(Transform startTransform, Vector3 start, Vector3 end, float duration) : base(startTransform, start, end, duration)
        {
            startTransform.localPosition = start;
        }

        public override (Vector3, Quaternion, Vector3) GetResult()
        {
            return (CurrentValue, StartTransform.localRotation, StartTransform.localScale);
        }
    }

    public class ScaleTweener : Vector3CustomTweener
    {
        public ScaleTweener(Transform startTransform, Vector3 start, Vector3 end, float duration) : base(startTransform, start, end, duration)
        {
            startTransform.localScale = start;
        }

        public override (Vector3, Quaternion, Vector3) GetResult()
        {
            return (StartTransform.localPosition, StartTransform.localRotation, CurrentValue);
        }
    }

    public class RotationTweener : CustomTweener<Quaternion, NoOptions>
    {
        public RotationTweener(Transform startTransform, Quaternion start, Quaternion end, float duration)
        {
            StartTransform = startTransform;
            startTransform.localRotation = start;
            core = CreateTweener(start, end, duration);
        }

        protected sealed override TweenerCore<Quaternion, Quaternion, NoOptions> CreateTweener(Quaternion start, Quaternion end, float duration)
        {
            CurrentValue = start;
            return DOTween.To(PureQuaternionPlugin.Plug(), () => CurrentValue, x => CurrentValue = x, end, duration);
        }

        public override (Vector3, Quaternion, Vector3) GetResult()
        {
            return (StartTransform.localPosition, CurrentValue, StartTransform.localScale);
        }
    }
    
    
    public struct SDKTweenComponent
    {
        public bool IsDirty { get; set; }
        public bool IsPlaying { get; set; }
        public Tweener Tweener { get; set; }
        public TweenStateStatus TweenStateStatus { get; set; }
        public ICustomTweener CustomTweener { get; set; }

    }
    
}
