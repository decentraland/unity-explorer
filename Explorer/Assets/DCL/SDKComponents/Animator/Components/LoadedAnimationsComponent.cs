using DCL.SDKComponents.Animator.Extensions;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace DCL.SDKComponents.Animator.Components
{
    //TODO dispose
    public readonly struct LoadedAnimationsComponent
    {
        public readonly List<LoadedAnimation> List;

        public LoadedAnimationsComponent(IReadOnlyList<Animation> list)
        {
            var mutableList = ListPool<LoadedAnimation>.Get()!;

            foreach (Animation animation in list)
            {
                animation.Initialize();
                mutableList.Add(new LoadedAnimation(animation));
            }

            this.List = mutableList;
        }
    }
}
