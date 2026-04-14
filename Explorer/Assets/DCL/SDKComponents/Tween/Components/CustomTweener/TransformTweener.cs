using DG.Tweening;
using UnityEngine;

namespace DCL.SDKComponents.Tween.Components
{
    /// <summary>
    /// Composite tweener that interpolates position, rotation, and scale simultaneously (MoveRotateScale).
    /// Uses a DOTween Sequence with Transform shortcuts.
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

        public void Initialize(Transform? transform,
            Vector3 positionStart, Vector3 positionEnd,
            Quaternion rotationStart, Quaternion rotationEnd,
            Vector3 scaleStart, Vector3 scaleEnd,
            float durationInSeconds)
        {
            activeTween?.Kill();
            finished = false;

            var seq = DOTween.Sequence();
            seq.Pause();

            if (transform)
            {
                seq.Join(
                    transform.DOLocalMove(positionEnd, durationInSeconds)
                        .From(positionStart, false)
                        .SetEase(Ease.Linear)
                        .SetAutoKill(false)
                        .Pause()
                );

                seq.Join(
                    transform.DOLocalRotateQuaternion(rotationEnd, durationInSeconds)
                        .From(rotationStart, false)
                        .SetEase(Ease.Linear)
                        .SetAutoKill(false)
                        .Pause()
                );

                seq.Join(
                    transform.DOScale(scaleEnd, durationInSeconds)
                        .From(scaleStart, false)
                        .SetEase(Ease.Linear)
                        .SetAutoKill(false)
                        .Pause()
                );
            }
            seq.SetAutoKill(false);
            seq.Pause();

            activeTween = seq;
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
