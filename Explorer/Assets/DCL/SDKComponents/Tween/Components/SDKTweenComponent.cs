using DCL.ECSComponents;
using DG.Tweening;

namespace ECS.Unity.Tween.Components
{
    public struct SDKTweenComponent
    {
        public bool IsDirty { get; set; }
        public bool Removed { get; set; }
        public bool IsPlaying { get; set; }
        public float CurrentTime { get; set; }
        public Tweener Tweener { get; set; }
        public TweenStateStatus TweenStateStatus { get; set; }
        public SDKTweenModel CurrentTweenModel { get; set; }
    }

    public struct SDKTweenModel
    {
        public EasingFunction EasingFunction;
        public PBTween.ModeOneofCase ModeCase;
        public float CurrentTime;
        public float Duration;
        public bool Playing;
        public Scale Scale;
        public Rotate Rotate;
        public Move Move;

        public void Update(PBTween pbTween)
        {
            EasingFunction = pbTween.EasingFunction;
            ModeCase = pbTween.ModeCase;
            CurrentTime = pbTween.CurrentTime;
            Duration = pbTween.Duration;
            Playing = !pbTween.HasPlaying || pbTween.Playing;
            Scale = pbTween.Scale;
            Rotate = pbTween.Rotate;
            Move = pbTween.Move;
        }
    }
}
