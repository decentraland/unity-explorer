using DCL.SDKComponents.Animator.Extensions;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.SDKComponents.Animator.Components
{
    public readonly struct LoadedAnimationsComponent
    {
        public readonly IReadOnlyList<Animation> List;

        public LoadedAnimationsComponent(IReadOnlyList<Animation> list)
        {
            foreach (Animation animation in list)
                animation.Initialize();

            this.List = list;
        }
    }
}
