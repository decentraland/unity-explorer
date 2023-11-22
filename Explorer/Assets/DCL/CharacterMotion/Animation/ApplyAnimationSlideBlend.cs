using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.CharacterMotion.Components;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace DCL.CharacterMotion.Animation
{
    public static class ApplyAnimationSlideBlend
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Execute(
            float dt,
            ref CharacterAnimationComponent animationComponent,
            in CharacterRigidTransform rigidTransform,
            in IAvatarView view)
        {
            int targetSlideBlend = rigidTransform.IsOnASteepSlope ? 1 : 0;
            animationComponent.States.SlideBlendValue = Mathf.MoveTowards(animationComponent.States.SlideBlendValue, targetSlideBlend, 3 * dt);
            view.SetAnimatorFloat(AnimationHashes.SLIDE_BLEND, animationComponent.States.SlideBlendValue);
        }
    }
}
