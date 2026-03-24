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

        protected sealed override DG.Tweening.Tween CreateTweener(Vector3 start, Vector3 end, float duration)
        {
            CurrentValue = start;
            return DOTween.To(getValue, setValue, end, duration);
        }

        protected override DG.Tweening.Tween CreateContinuousTweener(Vector3 start, Vector3 direction, float speed)
        {
            Vector3 dir = direction.normalized;
            float absSpeed = Mathf.Abs(speed);
            float sign = speed >= 0 ? 1f : -1f;

            return DOVirtual.Float(0f, 1f, 1f, v =>
            {
                CurrentValue = start + dir * (sign * absSpeed * v);
            }).SetLoops(-1, LoopType.Incremental);
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

        protected override DG.Tweening.Tween CreateTweener(Quaternion start, Quaternion end, float duration)
        {
            CurrentValue = start;
            return DOTween.To(PureQuaternionPlugin.Plug(), getValue, setValue, end, duration);
        }

        protected override DG.Tweening.Tween CreateContinuousTweener(Quaternion start, Quaternion direction, float speed)
        {
            // Derive rotation axis directly from the quaternion's imaginary part (x,y,z = sin(angle/2) * axis).
            // This correctly preserves the sign of the axis (e.g. +Y vs -Y) and avoids the identity problem
            // where any rotation around Y would leave Vector3.up unchanged and lose direction information.
            var axis = new Vector3(direction.x, direction.y, direction.z);
            if (axis.sqrMagnitude < 1e-6f)
                axis = Vector3.up;
            else
                axis = axis.normalized;

            float absSpeed = Mathf.Abs(speed);
            float secondsPerRevolution = 360f / Mathf.Max(absSpeed, 0.0001f);
            float sign = speed >= 0 ? 1f : -1f;

            DG.Tweening.Tween t = DOVirtual.Float(
                0f,
                360f,
                secondsPerRevolution,
                v => { CurrentValue = Quaternion.AngleAxis(sign * v, axis) * start; }
            ).SetLoops(-1, LoopType.Restart);

            return t;
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

        protected sealed override DG.Tweening.Tween CreateTweener(Vector2 start, Vector2 end, float duration)
        {
            CurrentValue = start;
            return DOTween.To(getValue, setValue, end, duration);
        }

        protected override DG.Tweening.Tween CreateContinuousTweener(Vector2 start, Vector2 direction, float speed)
        {
            Vector2 dir = direction.normalized;
            float absSpeed = Mathf.Abs(speed);
            float sign = speed >= 0 ? 1f : -1f;

            return DOVirtual.Float(0f, 1f, 1f, v =>
            {
                CurrentValue = start + dir * (sign * absSpeed * v);
            }).SetLoops(-1, LoopType.Incremental);
        }
    }
}
