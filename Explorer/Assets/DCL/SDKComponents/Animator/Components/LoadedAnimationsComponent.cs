using DCL.ECSComponents;
using DCL.SDKComponents.Animator.Extensions;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace DCL.SDKComponents.Animator.Components
{
    public struct LoadedAnimationsComponent : IDisposable
    {
        public readonly List<LoadedAnimation> List;
        public string? PlayingAnimationClipName;

        public LoadedAnimationsComponent(IReadOnlyList<Animation> list)
        {
            var mutableList = ListPool<LoadedAnimation>.Get()!;

            foreach (Animation animation in list)
            {
                animation.Initialize();
                mutableList.Add(new LoadedAnimation(animation));
            }

            this.List = mutableList;
            PlayingAnimationClipName = null;
        }

        public void Apply(PBAnimationState? playingAnimation, PBAnimationState? stoppedAnimation)
        {
            this.PlayingAnimationClipName = playingAnimation?.Clip;

            foreach (var animation in List)
            {
                animation.Apply(playingAnimation, stoppedAnimation);
                animation.Update();
            }
        }

        public readonly void Dispose()
        {
            ListPool<LoadedAnimation>.Release(List);
        }
    }
}
