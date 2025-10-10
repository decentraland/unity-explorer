
namespace DCL.SDKComponents.Tween.Components
{
    public interface ISequenceTweener
    {
        void Play();

        void Pause();

        void Rewind();

        void Kill(bool complete);

        bool IsPaused();

        bool IsFinished();

        bool IsActive();
    }
}





