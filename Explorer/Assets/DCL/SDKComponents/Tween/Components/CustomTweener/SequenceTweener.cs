using CrdtEcsBridge.Components.Conversion;
using DG.Tweening;
using DG.Tweening.Core;
using DG.Tweening.Plugins.Options;
using DCL.ECSComponents;
using DCL.AvatarRendering.AvatarShape.Rendering.TextureArray;
using DCL.SDKComponents.Tween;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.SDKComponents.Tween.Components
{
    public class SequenceTweener : ITweener
    {
        private readonly TweenCallback onCompleteCallback;
        private bool finished;
        private Sequence sequence;

        /// <summary>
        /// Resolved MoveRotateScale values filled by callback when a MoveRotateScale step starts.
        /// </summary>
        private ResolvedMoveRotateScale resolvedMoveRotateScale;

        /// <summary>
        /// Resolved MoveRotateScaleContinuous directions filled by callback when a MoveRotateScaleContinuous step starts.
        /// </summary>
        private ResolvedMoveRotateScaleContinuous resolvedMoveRotateScaleContinuous;

        /// <summary>
        /// Start transform when a MoveRotateScaleContinuous step begins (for finite-duration continuous).
        /// </summary>
        private Vector3 moveRotateScaleContinuousStartPosition;
        private Quaternion moveRotateScaleContinuousStartRotation;
        private Vector3 moveRotateScaleContinuousStartScale;

        /// <summary>
        /// Precomputed rotation axis and angle for MoveRotateScaleContinuous (set in callback once per step).
        /// Avoids ToAngleAxis + normalized per frame.
        /// </summary>
        private float moveRotateScaleContinuousRotAngle;
        private Vector3 moveRotateScaleContinuousRotAxis;

        public SequenceTweener()
        {
            onCompleteCallback = OnSequenceComplete;
        }

        public void Initialize(PBTween firstTween, IEnumerable<PBTween> additionalTweens, TweenLoop? loopType, Transform transform, Material? material = null)
        {
            sequence?.Kill();
            finished = false;
            sequence = DOTween.Sequence();
            sequence.Pause();

            AppendTweenStep(firstTween, firstTween.Duration / 1000f, transform, material);

            foreach (PBTween pbTween in additionalTweens)
                AppendTweenStep(pbTween, pbTween.Duration / 1000f, transform, material);

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

        private void AppendTweenStep(PBTween pbTween, float durationInSeconds, Transform transform, Material? material)
        {
            Ease ease = TweenSDKComponentHelper.GetEase(pbTween.EasingFunction);

            if (pbTween.ModeCase == PBTween.ModeOneofCase.MoveRotateScale)
            {
                MoveRotateScale moveRotateScale = pbTween.MoveRotateScale;
                sequence.AppendCallback(() => TweenSDKComponentHelper.ResolveMoveRotateScale(moveRotateScale, transform, out resolvedMoveRotateScale));
                DG.Tweening.Tween tween = DOVirtual.Float(0f, 1f, durationInSeconds, t =>
                {
                    ResolvedMoveRotateScale r = resolvedMoveRotateScale;
                    transform.localPosition = Vector3.Lerp(r.PositionStart, r.PositionEnd, t);
                    transform.localRotation = Quaternion.Slerp(r.RotationStart, r.RotationEnd, t);
                    transform.localScale = Vector3.Lerp(r.ScaleStart, r.ScaleEnd, t);
                }).SetAutoKill(false).Pause();
                tween.SetEase(ease);
                sequence.Append(tween);
                return;
            }

            if (pbTween.ModeCase == PBTween.ModeOneofCase.MoveRotateScaleContinuous)
            {
                MoveRotateScaleContinuous moveRotateScaleContinuous = pbTween.MoveRotateScaleContinuous;
                float speed = moveRotateScaleContinuous.Speed;
                sequence.AppendCallback(() =>
                {
                    TweenSDKComponentHelper.ResolveMoveRotateScaleContinuous(moveRotateScaleContinuous, out resolvedMoveRotateScaleContinuous);
                    moveRotateScaleContinuousStartPosition = transform.localPosition;
                    moveRotateScaleContinuousStartRotation = transform.localRotation;
                    moveRotateScaleContinuousStartScale = transform.localScale;
                    // Precompute rotation axis/angle once (fixed for this step); used inside DOVirtual to avoid per-frame ToAngleAxis + normalized.
                    resolvedMoveRotateScaleContinuous.RotationDirection.ToAngleAxis(out moveRotateScaleContinuousRotAngle, out moveRotateScaleContinuousRotAxis);
                    if (moveRotateScaleContinuousRotAxis.sqrMagnitude < 1e-6f)
                        moveRotateScaleContinuousRotAxis = Vector3.up;
                    moveRotateScaleContinuousRotAxis = moveRotateScaleContinuousRotAxis.normalized;
                });
                if (durationInSeconds > 0f)
                {
                    float totalTime = durationInSeconds;
                    DG.Tweening.Tween tween = DOVirtual.Float(0f, 1f, totalTime, v =>
                    {
                        float t = speed * totalTime * v;
                        transform.localPosition = moveRotateScaleContinuousStartPosition + resolvedMoveRotateScaleContinuous.PositionDirection * t;
                        transform.localRotation = Quaternion.AngleAxis(moveRotateScaleContinuousRotAngle * t, moveRotateScaleContinuousRotAxis) * moveRotateScaleContinuousStartRotation;
                        transform.localScale = moveRotateScaleContinuousStartScale + resolvedMoveRotateScaleContinuous.ScaleDirection * t;
                    }).SetEase(Ease.Linear).SetAutoKill(false).Pause();
                    sequence.Append(tween);
                }
                else
                {
                    float absSpeed = Mathf.Abs(speed);
                    float sign = speed >= 0 ? 1f : -1f;

                    // A single standalone tween drives all three properties.
                    // Standalone tweens fully support infinite loops; DOTween Sequences do not
                    // (they silently cap nested infinite loops to 1).
                    DG.Tweening.Tween tween = DOVirtual.Float(0f, 1f, 1f, v =>
                    {
                        float t = sign * absSpeed * v;
                        transform.localPosition = moveRotateScaleContinuousStartPosition + resolvedMoveRotateScaleContinuous.PositionDirection * t;
                        transform.localRotation = Quaternion.AngleAxis(moveRotateScaleContinuousRotAngle * t, moveRotateScaleContinuousRotAxis) * moveRotateScaleContinuousStartRotation;
                        transform.localScale = moveRotateScaleContinuousStartScale + resolvedMoveRotateScaleContinuous.ScaleDirection * t;
                    })
                   .SetEase(Ease.Linear)
                   .SetLoops(-1, LoopType.Incremental)
                   .SetAutoKill(false)
                   .Pause();
                    sequence.Append(tween);
                }
                return;
            }

            DG.Tweening.Tween? stepTween = CreateTweenForPBTween(pbTween, durationInSeconds, transform, material);
            if (stepTween != null)
            {
                stepTween.SetEase(ease);
                sequence.Append(stepTween);
            }
        }

        private DG.Tweening.Tween? CreateTweenForPBTween(PBTween pbTween, float durationInSeconds, Transform transform, Material? material)
        {
            DG.Tweening.Tween? returnTween = null;

            switch (pbTween.ModeCase)
            {
                case PBTween.ModeOneofCase.Move:
                    returnTween = transform.DOLocalMove(pbTween.Move.End, durationInSeconds)
                                         .From(pbTween.Move.Start, false)
                                           .SetAutoKill(false).Pause();
                    break;

                case PBTween.ModeOneofCase.Rotate:
                    returnTween = transform.DOLocalRotateQuaternion(pbTween.Rotate.End.ToUnityQuaternion(), durationInSeconds)
                                           .From(pbTween.Rotate.Start.ToUnityQuaternion(), false)
                                           .SetAutoKill(false).Pause();
                    break;

                case PBTween.ModeOneofCase.Scale:
                    returnTween = transform.DOScale(pbTween.Scale.End, durationInSeconds)
                                           .From(pbTween.Scale.Start, false)
                                           .SetAutoKill(false).Pause();
                    break;

                case PBTween.ModeOneofCase.TextureMove:
                    if (material != null)
                    {
                        int propertyId = TextureArrayConstants.BASE_MAP_ORIGINAL_TEXTURE;
                        TweenerCore<Vector2, Vector2, VectorOptions> textureTweener = null;

                        switch (pbTween.TextureMove.MovementType)
                        {
                            case TextureMovementType.TmtOffset:
                                textureTweener = DOTween.To(() => material.GetTextureOffset(propertyId), x => material.SetTextureOffset(propertyId, x), pbTween.TextureMove.End, durationInSeconds);
                                break;
                            case TextureMovementType.TmtTiling:
                                textureTweener = DOTween.To(() => material.GetTextureScale(propertyId), x => material.SetTextureScale(propertyId, x), pbTween.TextureMove.End, durationInSeconds);
                                break;
                        }

                        if (textureTweener != null)
                        {
                            textureTweener.From(pbTween.TextureMove.Start, false, false);
                            textureTweener.SetAutoKill(false).Pause();
                            returnTween = textureTweener;
                        }
                    }
                    break;
            }

            return returnTween;
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

        public void DoTween(Ease ease, float tweenModelCurrentTime, bool isPlaying)
        {
            // Sequences don't use DoTween - they're configured in Initialize()
            // This is just here to satisfy the ITweener interface
        }

        private void OnSequenceComplete()
        {
            finished = true;
        }
    }
}

