using CrdtEcsBridge.Components.Transform;
using DCL.ECSComponents;
using DG.Tweening;
using UnityEngine;

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

        void UpdateSDKTransform(ref SDKTransform sdkTransform);

        void UpdateTransform(Transform transform);

        void UpdateMaterial(SDKTweenTextureComponent textureComponent, Material material);
    }
}
