using DG.Tweening;

namespace DCL.SDKComponents.Tween.Components
{
    public interface ITweener
    {
        void DoTween(Ease ease, float tweenModelCurrentTime, bool isPlaying);

        void Play();

        void Pause();

        void Rewind();

        void Kill(bool complete);

        bool IsPaused();

        bool IsFinished();

        bool IsActive();

        public float GetElapsedTime();
    }
}
