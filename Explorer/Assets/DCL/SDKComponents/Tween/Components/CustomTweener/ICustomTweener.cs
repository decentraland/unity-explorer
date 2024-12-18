using DCL.ECSComponents;
using DG.Tweening;

namespace DCL.SDKComponents.Tween.Components
{
    public interface ICustomTweener
    {
        void Initialize(PBTween pbTween, float durationInSeconds);

        void DoTween(Ease ease, float tweenModelCurrentTime, bool isPlaying);

        void Play();

        void Pause();

        void Rewind();

        bool IsPaused();

        bool IsFinished();

        bool IsActive();
    }

    public interface ICustomTweener<T> : ICustomTweener
    {
        public T CurrentValue { get; set; }
    }
}
