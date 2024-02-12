using Arch.Core;
using DCL.ECSComponents;
using DG.Tweening;
using UnityEngine;

namespace ECS.Unity.Tween.Components
{
    public struct SDKTweenComponent
    {
        public SDKTweenComponent(Entity globalWorldEntity)
        {
            this.globalWorldEntity = globalWorldEntity;
            removed = false;
            playing = false;
            currentTime = 0;
            tweener = null;
            tweenMode = PBTween.ModeOneofCase.None;
            currentTweenModel = null;
            dirty = false;
            currentTweenSequence = null;
        }

        public Entity globalWorldEntity;

        public SDKTweenComponent(Entity globalWorldEntity, bool removed, bool playing, float currentTime,
            Tweener tweener, PBTween.ModeOneofCase tweenMode, PBTween currentTweenModel, bool dirty)
        {
            this.globalWorldEntity = globalWorldEntity;
            this.removed = removed;
            this.playing = playing;
            this.currentTime = currentTime;
            this.tweener = tweener;
            this.tweenMode = tweenMode;
            this.currentTweenModel = currentTweenModel;
            this.dirty = dirty;
        }

        public bool dirty { get; set; }
        public bool removed;
        public bool playing;
        public float currentTime;
        public Tweener tweener;
        public PBTween.ModeOneofCase tweenMode;
        public PBTween currentTweenModel;
        public PBTweenSequence currentTweenSequence;
    }
}
