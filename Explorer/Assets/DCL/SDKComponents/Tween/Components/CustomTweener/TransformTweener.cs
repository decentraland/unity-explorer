using CrdtEcsBridge.Components.Conversion;
using DG.Tweening;
using UnityEngine;

namespace DCL.SDKComponents.Tween.Components
{
    /// <summary>
    /// Composite tweener that interpolates position, rotation, and scale simultaneously.
    /// For finite tweens (MoveRotateScale): uses a DOTween Sequence with Transform shortcuts.
    /// For continuous tweens (MoveRotateScaleContinuous): uses a single standalone DOVirtual.Float
    /// with infinite loops, since DOTween silently caps infinite loops to 1 on nested Sequence tweens.
    /// DOTween writes directly to the Unity Transform; the system reads back via SyncTransformToSDKTransform.
    /// </summary>
    public class TransformTweener : ITweener
    {
        private readonly TweenCallback onCompleteCallback;
        private bool finished;
        private DG.Tweening.Tween activeTween;

        public TransformTweener()
        {
            onCompleteCallback = OnComplete;
        }

        public void Initialize(Transform transform,
            Vector3 positionStart, Vector3 positionEnd,
            Quaternion rotationStart, Quaternion rotationEnd,
            Vector3 scaleStart, Vector3 scaleEnd,
            float durationInSeconds)
        {
            activeTween?.Kill();
            finished = false;

            var seq = DOTween.Sequence();
            seq.Pause();

            seq.Join(
                transform.DOLocalMove(positionEnd, durationInSeconds)
                    .From(positionStart, false)
                    .SetAutoKill(false)
                    .Pause()
            );

            seq.Join(
                transform.DOLocalRotateQuaternion(rotationEnd, durationInSeconds)
                    .From(rotationStart, false)
                    .SetAutoKill(false)
                    .Pause()
            );

            seq.Join(
                transform.DOScale(scaleEnd, durationInSeconds)
                    .From(scaleStart, false)
                    .SetAutoKill(false)
                    .Pause()
            );

            seq.SetAutoKill(false);
            seq.OnComplete(onCompleteCallback);
            seq.Pause();

            activeTween = seq;
        }

        public void InitializeContinuous(Transform transform,
            Vector3 positionDirection, Quaternion rotationDirection, Vector3 scaleDirection,
            float speed)
        {
            activeTween?.Kill();
            finished = false;

            float absSpeed = Mathf.Abs(speed);
            float sign = speed >= 0 ? 1f : -1f;

            Vector3 startPos = transform.localPosition;
            Quaternion startRot = transform.localRotation;
            Vector3 startScale = transform.localScale;

            // Extract axis and angle from the quaternion so the angle component
            // scales rotation rate relative to speed, just as direction magnitude
            // scales position/scale rate.
            rotationDirection.ToAngleAxis(out float rotAngle, out Vector3 rotAxis);
            if (rotAxis.sqrMagnitude < 1e-6f)
                rotAxis = Vector3.up;
            rotAxis = rotAxis.normalized;

            // A single standalone tween drives all three properties.
            // Standalone tweens fully support infinite loops; DOTween Sequences do not
            // (they silently cap nested infinite loops to 1).
            // Direction magnitudes are intentionally preserved (not normalized) so users
            // can control relative speeds across position/rotation/scale via a single
            // shared speed value.
            activeTween = DOVirtual.Float(0f, 1f, 1f, v =>
            {
                float t = sign * absSpeed * v;
                transform.localPosition = startPos + positionDirection * t;
                transform.localRotation = Quaternion.AngleAxis(rotAngle * t, rotAxis) * startRot;
                transform.localScale = startScale + scaleDirection * t;
            })
            .SetEase(Ease.Linear)
            .SetLoops(-1, LoopType.Incremental)
            .SetAutoKill(false)
            .OnComplete(onCompleteCallback);

            activeTween.Pause();
        }

        public void DoTween(Ease ease, float tweenModelCurrentTime, bool isPlaying)
        {
            activeTween?.SetEase(ease).SetAutoKill(false).OnComplete(onCompleteCallback).Goto(tweenModelCurrentTime, isPlaying);
        }

        public void Play() =>
            activeTween?.Play();

        public void Pause() =>
            activeTween?.Pause();

        public void Rewind() =>
            activeTween?.Rewind();

        public void Kill(bool complete)
        {
            activeTween?.Kill(complete);
            finished = complete;
        }

        public bool IsPaused() =>
            !activeTween?.IsPlaying() ?? false;

        public bool IsFinished() =>
            finished;

        public bool IsActive() =>
            !IsFinished() && (activeTween?.IsPlaying() ?? false);

        public float GetElapsedTime() =>
            activeTween?.Elapsed() ?? 0f;

        private void OnComplete()
        {
            finished = true;
        }
    }
}
