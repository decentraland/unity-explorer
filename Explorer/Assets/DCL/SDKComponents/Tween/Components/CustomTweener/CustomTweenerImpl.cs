using DG.Tweening;
using DG.Tweening.Core;
using DG.Tweening.CustomPlugins;
using DG.Tweening.Plugins.Options;
using UnityEngine;

namespace DCL.SDKComponents.Tween.Components
{
    public class Vector3Tweener : CustomTweener<Vector3, VectorOptions>
    {
        private readonly DOGetter<Vector3> getValue;
        private readonly DOSetter<Vector3> setValue;

        public Vector3Tweener()
        {
            getValue = GetCurrentValue;
            setValue = SetCurrentValue;
        }

        private Vector3 GetCurrentValue() => CurrentValue;
        private void SetCurrentValue(Vector3 value) => CurrentValue = value;

        protected sealed override TweenerCore<Vector3, Vector3, VectorOptions> CreateTweener(Vector3 start, Vector3 end, float duration)
        {
            CurrentValue = start;
            return DOTween.To(getValue, setValue, end, duration);
        }
    }

    public class QuaternionTweener : CustomTweener<Quaternion, NoOptions>
    {
        private readonly DOGetter<Quaternion> getValue;
        private readonly DOSetter<Quaternion> setValue;

        public QuaternionTweener()
        {
            getValue = GetCurrentValue;
            setValue = SetCurrentValue;
        }

        private Quaternion GetCurrentValue() => CurrentValue;
        private void SetCurrentValue(Quaternion value) => CurrentValue = value;

        protected override TweenerCore<Quaternion, Quaternion, NoOptions> CreateTweener(Quaternion start, Quaternion end, float duration)
        {
            CurrentValue = start;
            return DOTween.To(PureQuaternionPlugin.Plug(), getValue, setValue, end, duration);
        }
    }

    public class Vector2Tweener : CustomTweener<Vector2, VectorOptions>
    {
        private readonly DOGetter<Vector2> getValue;
        private readonly DOSetter<Vector2> setValue;

        public Vector2Tweener()
        {
            getValue = GetCurrentValue;
            setValue = SetCurrentValue;
        }

        private Vector2 GetCurrentValue() => CurrentValue;
        private void SetCurrentValue(Vector2 value) => CurrentValue = value;

        protected sealed override TweenerCore<Vector2, Vector2, VectorOptions> CreateTweener(Vector2 start, Vector2 end, float duration)
        {
            CurrentValue = start;
            return DOTween.To(getValue, setValue, end, duration);
        }
    }
}
