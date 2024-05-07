using DCL.ECSComponents;
using DCL.SDKComponents.Animator.Extensions;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace DCL.SDKComponents.Animator.Components
{
    //TODO dispose
    public struct LoadedAnimationsComponent
    {
        public readonly List<LoadedAnimation> List;
        public string? playingAnimationClipName;

        public LoadedAnimationsComponent(IReadOnlyList<Animation> list)
        {
            var mutableList = ListPool<LoadedAnimation>.Get()!;

            foreach (Animation animation in list)
            {
                animation.Initialize();
                mutableList.Add(new LoadedAnimation(animation));
            }

            this.List = mutableList;
            playingAnimationClipName = null;
        }

        public void Apply(PBAnimationState? playingAnimation, PBAnimationState? stoppedAnimation)
        {
            this.playingAnimationClipName = playingAnimation?.Clip;

            foreach (var animation in List)
            {
                animation.Apply(playingAnimation, stoppedAnimation);
                animation.Update();
            }
        }
    }
}
