using DCL.ECSComponents;
using DCL.SDKComponents.Animator.Components;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.SDKComponents.Animator.Extensions
{
    public static class AnimationExtensions
    {
        private const int INITIAL_LAYER_INDEX = 0;

        public static void Initialize(this Animation animation)
        {
            animation.playAutomatically = true;
            animation.enabled = true;
            animation.Stop();

            //putting the component in play state if playAutomatically was true at that point.
            if (animation.clip)
                animation.clip!.SampleAnimation(animation.gameObject, 0);

            int layerIndex = INITIAL_LAYER_INDEX;

            foreach (AnimationState animationState in animation)
            {
                animationState.clip!.wrapMode = WrapMode.Loop;
                animationState.layer = layerIndex;
                animationState.blendMode = AnimationBlendMode.Blend;
                layerIndex++;
            }
        }

        public static void TryStop(this Animation animation, string clipName)
        {
            if (animation.IsPlaying(clipName))
                animation.Stop(clipName);
        }

        public static void TryPlay(this Animation animation, string clipName)
        {
            if (animation.IsPlaying(clipName) == false)
                animation.Play(clipName);
        }

        public static void ApplySettings(this Animation animation, PBAnimationState state)
        {
            AnimationState animationState = animation[state.Clip!]!;
            if (!animationState) return;
            new SDKAnimationState(state).ApplyOn(animationState);
        }
    }
}
