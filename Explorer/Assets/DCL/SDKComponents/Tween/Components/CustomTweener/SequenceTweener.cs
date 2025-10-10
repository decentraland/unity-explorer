using CrdtEcsBridge.Components.Conversion;
using DG.Tweening;
using DCL.ECSComponents;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.SDKComponents.Tween.Components
{
    public class SequenceTweener : ISequenceTweener
    {
        private readonly TweenCallback onCompleteCallback;
        private bool finished;
        private Sequence sequence;

        public SequenceTweener()
        {
            onCompleteCallback = OnSequenceComplete;
        }

        public void Initialize(PBTween firstTween, IEnumerable<PBTween> additionalTweens, TweenLoop? loopType, Transform transform)
        {
            sequence?.Kill();
            finished = false;
            sequence = DOTween.Sequence();
            sequence.Pause();

            // Add the first tween from PBTween component
            var firstDOTween = CreateTweenForPBTween(firstTween, firstTween.Duration / 1000f, transform);
            if (firstDOTween != null)
            {
                Ease firstEase = GetEase(firstTween.EasingFunction);
                firstDOTween.SetEase(firstEase);
                sequence.Append(firstDOTween);
            }

            // Add additional tweens from PBTweenSequence.sequence
            foreach (PBTween pbTween in additionalTweens)
            {
                var tween = CreateTweenForPBTween(pbTween, pbTween.Duration / 1000f, transform);
                if (tween != null)
                {
                    Ease ease = GetEase(pbTween.EasingFunction);
                    tween.SetEase(ease);
                    sequence.Append(tween);
                }
            }

            // Configure loop type only if specified - otherwise sequence plays once
            if (loopType.HasValue)
            {
                if (loopType.Value == TweenLoop.TlYoyo)
                    sequence.SetLoops(-1, LoopType.Yoyo);
                else if (loopType.Value == TweenLoop.TlRestart)
                    sequence.SetLoops(-1, LoopType.Restart);
            }

            sequence.SetAutoKill(false);
            sequence.OnComplete(onCompleteCallback);
            sequence.Pause();
        }

        private DG.Tweening.Tween? CreateTweenForPBTween(PBTween pbTween, float durationInSeconds, Transform transform)
        {
            DG.Tweening.Tween? returnTween = null;

            switch (pbTween.ModeCase)
            {
                case PBTween.ModeOneofCase.Move:
                    returnTween = transform.DOLocalMove(pbTween.Move.End, durationInSeconds)
                                         .From(pbTween.Move.Start, false)
                                           .SetAutoKill(false).Pause();
                    /*returnTween = DOTween.To(
                        () => transform.localPosition,
                        x => transform.localPosition = x,
                        pbTween.Move.End,
                        durationInSeconds
                    ).From(pbTween.Move.Start, false)
                   .SetAutoKill(false).Pause();*/
                    break;

                case PBTween.ModeOneofCase.Rotate:
                    returnTween = transform.DOLocalRotateQuaternion(pbTween.Rotate.End.ToUnityQuaternion(), durationInSeconds)
                                           .From(pbTween.Rotate.Start.ToUnityQuaternion(), false)
                                           .SetAutoKill(false).Pause();
                    /*float slerpTime = 0f;
                    returnTween = DOTween.To(
                        () => slerpTime,
                        x =>
                        {
                            slerpTime = x;

                            transform.rotation = Quaternion.Slerp(
                                pbTween.Rotate.Start.ToUnityQuaternion(),
                                pbTween.Rotate.End.ToUnityQuaternion(), slerpTime);
                        },
                        1f, // target value (100% of interpolation)
                        durationInSeconds
                    )
                   .SetAutoKill(false).Pause();*/
                    break;

                case PBTween.ModeOneofCase.Scale:
                    returnTween = transform.DOScale(pbTween.Scale.End, durationInSeconds)
                                           .From(pbTween.Scale.Start, false)
                                           .SetAutoKill(false).Pause();
                    /*returnTween = DOTween.To(
                        () => transform.localScale,
                        x => transform.localScale = x,
                        pbTween.Scale.End,
                        durationInSeconds
                    ).From(pbTween.Scale.Start, false)
                   .SetAutoKill(false).Pause();*/
                    break;
            }

            return returnTween;
        }

        private Ease GetEase(EasingFunction easingFunction)
        {
            return easingFunction switch
            {
                EasingFunction.EfLinear => Ease.Linear,
                EasingFunction.EfEaseinsine => Ease.InSine,
                EasingFunction.EfEaseoutsine => Ease.OutSine,
                EasingFunction.EfEasesine => Ease.InOutSine,
                EasingFunction.EfEaseinquad => Ease.InQuad,
                EasingFunction.EfEaseoutquad => Ease.OutQuad,
                EasingFunction.EfEasequad => Ease.InOutQuad,
                EasingFunction.EfEaseinexpo => Ease.InExpo,
                EasingFunction.EfEaseoutexpo => Ease.OutExpo,
                EasingFunction.EfEaseexpo => Ease.InOutExpo,
                EasingFunction.EfEaseinelastic => Ease.InElastic,
                EasingFunction.EfEaseoutelastic => Ease.OutElastic,
                EasingFunction.EfEaseelastic => Ease.InOutElastic,
                EasingFunction.EfEaseinbounce => Ease.InBounce,
                EasingFunction.EfEaseoutbounce => Ease.OutBounce,
                EasingFunction.EfEasebounce => Ease.InOutBounce,
                EasingFunction.EfEaseincubic => Ease.InCubic,
                EasingFunction.EfEaseoutcubic => Ease.OutCubic,
                EasingFunction.EfEasecubic => Ease.InOutCubic,
                EasingFunction.EfEaseinquart => Ease.InQuart,
                EasingFunction.EfEaseoutquart => Ease.OutQuart,
                EasingFunction.EfEasequart => Ease.InOutQuart,
                EasingFunction.EfEaseinquint => Ease.InQuint,
                EasingFunction.EfEaseoutquint => Ease.OutQuint,
                EasingFunction.EfEasequint => Ease.InOutQuint,
                EasingFunction.EfEaseincirc => Ease.InCirc,
                EasingFunction.EfEaseoutcirc => Ease.OutCirc,
                EasingFunction.EfEasecirc => Ease.InOutCirc,
                EasingFunction.EfEaseinback => Ease.InBack,
                EasingFunction.EfEaseoutback => Ease.OutBack,
                EasingFunction.EfEaseback => Ease.InOutBack,
                _ => Ease.Linear
            };
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

        private void OnSequenceComplete()
        {
            finished = true;
        }
    }
}

