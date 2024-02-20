using DCL.ECSComponents;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.SDKComponents.Tween.Components
{
    public class SDKAnimatorComponent
    {
        public bool IsDirty { get; set; }
        public readonly List<SDKAnimationState> SDKAnimationStates = new List<SDKAnimationState>();
        public SDKAnimation SDKAnimation;

        public SDKAnimatorComponent(List<SDKAnimationState> sdkAnimationStates)
        {
            SDKAnimationStates = sdkAnimationStates;
            IsDirty = false;
        }

        public SDKAnimatorComponent() { }
    }

    public class SDKAnimationState
    {
        public string Clip;
        public bool Playing;
        public float Weight;
        public float Speed;
        public bool Loop;
        public bool ShouldReset;

        //this is the content of the PBAnimationState
        public void Update(string clip, bool playing, float weight, float speed, bool loop, bool shouldReset)
        {
            Clip = clip;
            Playing = playing;
            Weight = weight;
            Speed = speed;
            Loop = loop;
            ShouldReset = shouldReset;
        }

        public SDKAnimationState() { }
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
