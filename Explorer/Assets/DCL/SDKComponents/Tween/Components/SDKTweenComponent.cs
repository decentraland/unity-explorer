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
        public bool removed;
        public bool isPlaying;
        public float CurrentTime;
        public Tweener tweener;
        public PBTween.ModeOneofCase tweenMode;
        public PBTween CurrentTweenModel;
        public TweenStateStatus TweenStateStatus;
        public bool IsTweenStateDirty;
    }
}
