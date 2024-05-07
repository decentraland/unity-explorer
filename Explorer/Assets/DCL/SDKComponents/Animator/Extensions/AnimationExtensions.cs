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

        public static void SetAnimationState(this Animation animation, IReadOnlyList<PBAnimationState> sdkAnimationStates)
        {
            for (var i = 0; i < sdkAnimationStates.Count; i++)
            {
                var sdkAnimationState = new SDKAnimationState(sdkAnimationStates[i]!);
                AnimationState animationState = animation[sdkAnimationState.Clip]!;

                if (!animationState) continue;

                sdkAnimationState.ApplyOn(animationState);

                if (sdkAnimationState.ShouldReset && animation.IsPlaying(sdkAnimationState.Clip))
                {
                    animation.Stop(sdkAnimationState.Clip);

                    //Manually sample the animation. If the reset is not played again the frame 0 wont be applied
                    animationState.clip.SampleAnimation(animation.gameObject, 0);
                }

                if (sdkAnimationState.Playing && !animation.IsPlaying(sdkAnimationState.Clip))
                    animation.Play(sdkAnimationState.Clip);
            }
        }
    }
}
