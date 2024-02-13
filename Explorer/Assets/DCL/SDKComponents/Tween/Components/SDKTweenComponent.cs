using Arch.Core;
using DCL.ECSComponents;
using DG.Tweening;
using UnityEngine;

namespace ECS.Unity.Tween.Components
{
    public struct SDKTweenComponent
    {
        public Entity globalWorldEntity;

        public bool IsDirty { get; set; }
        public bool Removed { get; set; }
        public bool isPlaying { get; set; }
        public float CurrentTime { get; set; }
        public Tweener Tweener { get; set; }
        public PBTween.ModeOneofCase tweenMode { get; set; }
        public PBTween CurrentTweenModel { get; set; }
        public TweenStateStatus TweenStateStatus { get; set; }
        public bool IsTweenStateDirty { get; set; }
    }
}
