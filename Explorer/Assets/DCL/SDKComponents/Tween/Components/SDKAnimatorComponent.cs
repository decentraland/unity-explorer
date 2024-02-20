using DCL.ECSComponents;
using DCL.SDKComponents.Tween.Systems;
using System.Collections.Generic;

namespace DCL.SDKComponents.Animator.Components
{
    public struct SDKAnimatorComponent
    {
        public bool IsDirty;
        public readonly List<SDKAnimationState> SDKAnimationStates;

        public SDKAnimatorComponent(List<SDKAnimationState> sdkAnimationStates)
        {
            SDKAnimationStates = sdkAnimationStates;
            IsDirty = true;
        }
    }

    public readonly struct SDKAnimationState
    {
        public readonly string Clip;
        public readonly bool Playing;
        public readonly float Weight;
        public readonly float Speed;
        public readonly bool Loop;
        public readonly bool ShouldReset;

        public SDKAnimationState(PBAnimationState pbAnimationState)
        {
            Clip = pbAnimationState.Clip;
            Playing = pbAnimationState.Playing;
            Weight = pbAnimationState.GetWeight();
            Speed = pbAnimationState.GetSpeed();
            Loop = pbAnimationState.GetLoop();
            ShouldReset = pbAnimationState.GetShouldReset();
        }
    }
}
