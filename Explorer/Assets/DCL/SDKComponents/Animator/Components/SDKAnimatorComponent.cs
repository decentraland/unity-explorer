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
}