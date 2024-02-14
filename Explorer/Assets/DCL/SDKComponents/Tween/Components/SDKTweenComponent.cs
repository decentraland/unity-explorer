using DCL.ECSComponents;
using DG.Tweening;

namespace DCL.SDKComponents.Tween.Components
{
    public class SDKTweenComponent
    {
        public bool IsDirty { get; set; }
        public bool Removed { get; set; }
        public bool IsPlaying { get; set; }
        public float CurrentTime { get; set; }
        public Tweener Tweener { get; set; }
        public TweenStateStatus TweenStateStatus { get; set; }
        public SDKTweenModel CurrentTweenModel { get; set; }
    }

    public class SDKTweenModel
    {
        public EasingFunction EasingFunction;
        public PBTween.ModeOneofCase ModeCase;
        public float CurrentTime;
        public float Duration;
        public bool IsPlaying;
        public Scale Scale;
        public Rotate Rotate;
        public Move Move;

        public SDKTweenModel(PBTween pbTween)
        {
            Update(pbTween);
        }

        public void Update(PBTween pbTween)
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
