using DG.Tweening;
using UnityEngine;

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

        public Vector3? GetOffset();
    }
}
