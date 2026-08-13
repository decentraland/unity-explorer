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
        private readonly TweenCallback<float> onContinuousUpdate;

        // Per-tween continuous state, refreshed by CreateContinuousTweener so the update delegate is cached once (no per-setup closure allocation).
        private Vector3 continuousStart;
        private Vector3 continuousDir;
        private float continuousSignedSpeed;

        public Vector3Tweener()
        {
            getValue = GetCurrentValue;
            setValue = SetCurrentValue;
            onContinuousUpdate = OnContinuousUpdate;
        }

        private Vector3 GetCurrentValue() => CurrentValue;
        private void SetCurrentValue(Vector3 value) => CurrentValue = value;
        private void OnContinuousUpdate(float v) => CurrentValue = continuousStart + continuousDir * (continuousSignedSpeed * v);

        protected sealed override DG.Tweening.Tween CreateTweener(Vector3 start, Vector3 end, float duration)
        {
            CurrentValue = start;
            return DOTween.To(getValue, setValue, end, duration);
        }

        protected override DG.Tweening.Tween CreateContinuousTweener(Vector3 start, Vector3 direction, float speed)
        {
            float absSpeed = Mathf.Abs(speed);
            float sign = speed >= 0 ? 1f : -1f;

            continuousStart = start;
            continuousDir = direction.normalized;
            continuousSignedSpeed = sign * absSpeed;

            return DOVirtual.Float(0f, 1f, 1f, onContinuousUpdate).SetLoops(-1, LoopType.Incremental);
        }
    }

    public class QuaternionTweener : CustomTweener<Quaternion, NoOptions>
    {
        private readonly DOGetter<Quaternion> getValue;
        private readonly DOSetter<Quaternion> setValue;
        private readonly TweenCallback<float> onContinuousUpdate;

        // Per-tween continuous state, refreshed by CreateContinuousTweener so the update delegate is cached once (no per-setup closure allocation).
        private Vector3 continuousAxis;
        private float continuousSign;
        private Quaternion continuousStartRotation;

        public QuaternionTweener()
        {
            getValue = GetCurrentValue;
            setValue = SetCurrentValue;
            onContinuousUpdate = OnContinuousUpdate;
        }

        private Quaternion GetCurrentValue() => CurrentValue;
        private void SetCurrentValue(Quaternion value) => CurrentValue = value;
        private void OnContinuousUpdate(float v) => CurrentValue = Quaternion.AngleAxis(continuousSign * v, continuousAxis) * continuousStartRotation;

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
            axis = axis.sqrMagnitude < 1e-6f ? Vector3.up : axis.normalized;

            float absSpeed = Mathf.Abs(speed);
            float secondsPerRevolution = 360f / Mathf.Max(absSpeed, 0.0001f);
            float sign = speed >= 0 ? 1f : -1f;

            continuousAxis = axis;
            continuousSign = sign;
            continuousStartRotation = start;

            return DOVirtual.Float(
                0f,
                360f,
                secondsPerRevolution,
                onContinuousUpdate
            ).SetLoops(-1, LoopType.Restart);
        }
    }

    public class Vector2Tweener : CustomTweener<Vector2, VectorOptions>
    {
        private readonly DOGetter<Vector2> getValue;
        private readonly DOSetter<Vector2> setValue;
        private readonly TweenCallback<float> onContinuousUpdate;

        // Per-tween continuous state, refreshed by CreateContinuousTweener so the update delegate is cached once (no per-setup closure allocation).
        private Vector2 continuousStart;
        private Vector2 continuousDir;
        private float continuousSignedSpeed;

        public Vector2Tweener()
        {
            getValue = GetCurrentValue;
            setValue = SetCurrentValue;
            onContinuousUpdate = OnContinuousUpdate;
        }

        private Vector2 GetCurrentValue() => CurrentValue;
        private void SetCurrentValue(Vector2 value) => CurrentValue = value;
        private void OnContinuousUpdate(float v) => CurrentValue = continuousStart + continuousDir * (continuousSignedSpeed * v);

        protected sealed override DG.Tweening.Tween CreateTweener(Vector2 start, Vector2 end, float duration)
        {
            CurrentValue = start;
            return DOTween.To(getValue, setValue, end, duration);
        }

        protected override DG.Tweening.Tween CreateContinuousTweener(Vector2 start, Vector2 direction, float speed)
        {
            float absSpeed = Mathf.Abs(speed);
            float sign = speed >= 0 ? 1f : -1f;

            continuousStart = start;
            continuousDir = direction.normalized;
            continuousSignedSpeed = sign * absSpeed;

            return DOVirtual.Float(0f, 1f, 1f, onContinuousUpdate).SetLoops(-1, LoopType.Incremental);
        }
    }
}
