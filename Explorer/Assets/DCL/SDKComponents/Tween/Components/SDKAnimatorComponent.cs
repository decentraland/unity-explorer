using DCL.ECSComponents;
using DCL.SDKComponents.Tween.Systems;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.SDKComponents.Tween.Components
{
    public struct SDKAnimatorComponent
    {
        public bool IsDirty { get; set; }
        public readonly List<SDKAnimationState> SDKAnimationStates;
        public SDKAnimation SDKAnimation;

        public SDKAnimatorComponent(List<SDKAnimationState> sdkAnimationStates)
        {
            SDKAnimationStates = sdkAnimationStates;
            IsDirty = true;
            SDKAnimation = default;
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

    public struct SDKAnimation
    {
        public bool dirty { get; set; }
        public readonly Animation Animation;
        public bool IsInitialized;

        public SDKAnimation(Animation animation)
        {
            Animation = animation;
            dirty = false;
            IsInitialized = false;
        }
    }


}
