using DCL.CharacterMotion.Components;
using UnityEngine;
using Utility.Animations;

namespace DCL.CharacterMotion.Animation
{
    public static class GliderPropAnimationLogic
    {
        public static void Execute(Animator animator, in CharacterAnimationComponent animationComponent)
        {
            animator.SetFloat(AnimationHashes.MOVEMENT_BLEND, animationComponent.States.MovementBlendValue);
            animator.SetBool(AnimationHashes.GLIDING, animationComponent.States.IsGliding);
            animator.SetFloat(AnimationHashes.GLIDE_BLEND, animationComponent.States.GlideBlendValue);
        }
    }
}
