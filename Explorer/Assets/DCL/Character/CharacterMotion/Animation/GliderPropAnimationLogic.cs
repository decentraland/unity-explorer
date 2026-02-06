using DCL.CharacterMotion.Components;
using UnityEngine;
using Utility.Animations;

namespace DCL.CharacterMotion.Animation
{
    public static class GliderPropAnimationLogic
    {
        public static void Execute(Animator animator, in CharacterAnimationComponent animationComponent)
        {
            var glideState = animationComponent.States.GlideState;
            bool isGliding = glideState is GlideStateValue.OPENING_PROP or GlideStateValue.GLIDING;

            animator.SetFloat(AnimationHashes.MOVEMENT_BLEND, animationComponent.States.MovementBlendValue);
            animator.SetBool(AnimationHashes.GLIDING, isGliding);
            animator.SetFloat(AnimationHashes.GLIDE_BLEND, animationComponent.States.GlideBlendValue);
        }
    }
}
