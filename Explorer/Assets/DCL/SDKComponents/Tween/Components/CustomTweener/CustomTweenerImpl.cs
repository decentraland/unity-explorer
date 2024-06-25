using DG.Tweening;
using DG.Tweening.Core;
using DG.Tweening.CustomPlugins;
using DG.Tweening.Plugins.Options;
using UnityEngine;

namespace DCL.SDKComponents.Tween.Components
{
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

        public abstract override TweenResult GetResult();
    }

    public class PositionTweener : Vector3CustomTweener
    {
        public PositionTweener(Transform startTransform, Vector3 start, Vector3 end, float duration) : base(startTransform, start, end, duration)
        {
            startTransform.localPosition = start;
        }

        public override TweenResult GetResult()
        {
            return new TweenResult
            {
                Position = CurrentValue, Rotation = StartTransform.localRotation, Scale = StartTransform.localScale
            };
        }
    }

    public class ScaleTweener : Vector3CustomTweener
    {
        public ScaleTweener(Transform startTransform, Vector3 start, Vector3 end, float duration) : base(startTransform, start, end, duration)
        {
            startTransform.localScale = start;
        }

        public override TweenResult GetResult()
        {
            return new TweenResult
            {
                Position = StartTransform.localPosition, Rotation = StartTransform.localRotation, Scale = CurrentValue
            };
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

        public override TweenResult GetResult()
        {
            return new TweenResult
            {
                Position = StartTransform.localPosition, Rotation = CurrentValue, Scale = StartTransform.localScale
            };
        }
    }
}