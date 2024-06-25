using CRDT;
using DG.Tweening;
using UnityEngine;

namespace DCL.SDKComponents.Tween.Components
{
    public interface ICustomTweener
    {
        public TweenResult GetResult();
        public CRDTEntity ParentId { get; set; }
        void DoTween(Ease ease, float tweenModelCurrentTime, bool isPlaying);

        void Play();
        void Pause();
        void Kill();
        void Rewind();
        bool IsPaused();
        bool IsFinished();
        bool IsActive();
    }

    public struct TweenResult
    {
        public Vector3 Position;
        public Quaternion Rotation;
        public Vector3 Scale;
    }
}