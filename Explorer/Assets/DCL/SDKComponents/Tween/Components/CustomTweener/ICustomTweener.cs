using CrdtEcsBridge.Components.Transform;
using DCL.ECSComponents;
using DG.Tweening;
using UnityEngine;

namespace DCL.SDKComponents.Tween.Components
{
    public interface ICustomTweener
    {
        void DoTween(Ease ease, float tweenModelCurrentTime, bool isPlaying);
        void Play();
        void Pause();
        void Rewind();
        bool IsPaused();
        bool IsFinished();
        bool IsActive();

        void Initialize(PBTween pbTween, SDKTransform sdkTransform, float durationInSeconds);
        void SetResult(ref SDKTransform sdkTransform);
    }
}
