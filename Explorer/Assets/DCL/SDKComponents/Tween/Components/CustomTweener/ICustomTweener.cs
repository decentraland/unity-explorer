using DG.Tweening;

namespace DCL.SDKComponents.Tween.Components
{
    public interface ITweener
    {
        void DoTween(Ease ease, float tweenModelCurrentTime, bool isPlaying);

        void Play();

        void Pause();

        void Rewind();

        bool IsPaused();

        bool IsFinished();

        bool IsActive();
    }

    public interface ICustomTweener<T> : ITweener
    {
        public T CurrentValue { get; set; }

        void Initialize(T startValue, T endValue, float durationInSeconds);
    }
}
