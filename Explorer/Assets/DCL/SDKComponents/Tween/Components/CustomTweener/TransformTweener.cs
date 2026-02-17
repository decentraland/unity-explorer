using CrdtEcsBridge.Components.Conversion;
using DG.Tweening;
using UnityEngine;

namespace DCL.SDKComponents.Tween.Components
{
    /// <summary>
    /// Composite tweener that interpolates position, rotation, and scale simultaneously
    /// using DOTween Transform shortcuts joined in a parallel Sequence.
    /// DOTween writes directly to the Unity Transform; the system reads back via SyncTransformToSDKTransform.
    /// </summary>
    public class TransformTweener : ITweener
    {
        private readonly TweenCallback onCompleteCallback;
        private bool finished;
        private Sequence sequence;

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
            sequence?.Kill();
            finished = false;
            sequence = DOTween.Sequence();
            sequence.Pause();

            sequence.Join(
                transform.DOLocalMove(positionEnd, durationInSeconds)
                    .From(positionStart, false)
                    .SetAutoKill(false)
                    .Pause()
            );

            sequence.Join(
                transform.DOLocalRotateQuaternion(rotationEnd, durationInSeconds)
                    .From(rotationStart, false)
                    .SetAutoKill(false)
                    .Pause()
            );

            sequence.Join(
                transform.DOScale(scaleEnd, durationInSeconds)
                    .From(scaleStart, false)
                    .SetAutoKill(false)
                    .Pause()
            );

            sequence.SetAutoKill(false);
            sequence.OnComplete(onCompleteCallback);
            sequence.Pause();
        }

        public void DoTween(Ease ease, float tweenModelCurrentTime, bool isPlaying)
        {
            sequence?.SetEase(ease).SetAutoKill(false).OnComplete(onCompleteCallback).Goto(tweenModelCurrentTime, isPlaying);
        }

        public void Play() =>
            sequence?.Play();

        public void Pause() =>
            sequence?.Pause();

        public void Rewind() =>
            sequence?.Rewind();

        public void Kill(bool complete)
        {
            sequence?.Kill(complete);
            finished = complete;
        }

        public bool IsPaused() =>
            !sequence?.IsPlaying() ?? false;

        public bool IsFinished() =>
            finished;

        public bool IsActive() =>
            !IsFinished() && (sequence?.IsPlaying() ?? false);

        public float GetElapsedTime() =>
            sequence?.Elapsed() ?? 0f;

        private void OnComplete()
        {
            finished = true;
        }
    }
}
