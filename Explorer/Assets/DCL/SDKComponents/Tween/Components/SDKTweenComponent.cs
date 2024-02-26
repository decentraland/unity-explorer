using DCL.ECSComponents;
using DG.Tweening;

namespace DCL.SDKComponents.Tween.Components
{
    public struct SDKTweenComponent
    {
        public bool IsDirty { get; set; }
        public bool IsPlaying { get; set; }
        public float CurrentTime { get; set; }
        public Tweener Tweener { get; set; }
        public TweenStateStatus TweenStateStatus { get; set; }
        public SDKTweenModel CurrentTweenModel { get; set; }
    }

    public readonly struct SDKTweenModel
    {
        public readonly EasingFunction EasingFunction;
        public readonly PBTween.ModeOneofCase ModeCase;
        public readonly float CurrentTime;
        public readonly float Duration;
        public readonly bool IsPlaying;
        public readonly Scale Scale;
        public readonly Rotate Rotate;
        public readonly Move Move;

        public SDKTweenModel(PBTween pbTween)
        {
            EasingFunction = pbTween.EasingFunction;
            ModeCase = pbTween.ModeCase;
            CurrentTime = pbTween.CurrentTime;
            Duration = pbTween.Duration;
            IsPlaying = !pbTween.HasPlaying || pbTween.Playing;
            Scale = pbTween.Scale;
            Rotate = pbTween.Rotate;
            Move = pbTween.Move;
        }
    }
}
