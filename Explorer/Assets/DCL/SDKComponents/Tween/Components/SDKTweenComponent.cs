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
            transform = null;
            tweener = null;
            tweenMode = PBTween.ModeOneofCase.None;
            lastModel = null;
            dirty = false;
        }

        public Entity globalWorldEntity;

        public SDKTweenComponent(Entity globalWorldEntity, bool removed, bool playing, float currentTime, Transform transform,
            Sequence tweener, PBTween.ModeOneofCase tweenMode, PBTween lastModel, bool dirty)
        {
            this.globalWorldEntity = globalWorldEntity;
            this.removed = removed;
            this.playing = playing;
            this.currentTime = currentTime;
            this.transform = transform;
            this.tweener = tweener;
            this.tweenMode = tweenMode;
            this.lastModel = lastModel;
            this.dirty = dirty;
        }

        public bool dirty { get; set; }
        public bool removed;
        public bool playing;
        public float currentTime;
        public Transform transform;
        public Sequence tweener;
        public PBTween.ModeOneofCase tweenMode;
        public PBTween lastModel;
    }
}
