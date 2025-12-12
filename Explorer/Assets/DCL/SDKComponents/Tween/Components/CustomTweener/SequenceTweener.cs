using CrdtEcsBridge.Components.Conversion;
using DG.Tweening;
using DG.Tweening.Core;
using DG.Tweening.Plugins.Options;
using DCL.ECSComponents;
using DCL.AvatarRendering.AvatarShape.Rendering.TextureArray;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.SDKComponents.Tween.Components
{
    public class SequenceTweener : ITweener
    {
        private readonly TweenCallback onCompleteCallback;
        private bool finished;
        private Sequence sequence;

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

            // Add the first tween from PBTween component
            var firstDOTween = CreateTweenForPBTween(firstTween, firstTween.Duration / 1000f, transform, material);
            if (firstDOTween != null)
            {
                Ease firstEase = TweenSDKComponentHelper.GetEase(firstTween.EasingFunction);
                firstDOTween.SetEase(firstEase);
                sequence.Append(firstDOTween);
            }

            // Add additional tweens from PBTweenSequence.sequence
            foreach (PBTween pbTween in additionalTweens)
            {
                var tween = CreateTweenForPBTween(pbTween, pbTween.Duration / 1000f, transform, material);
                if (tween != null)
                {
                    Ease ease = TweenSDKComponentHelper.GetEase(pbTween.EasingFunction);
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

