using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.Character.CharacterMotion.Components;
using DCL.CharacterMotion.Components;
using DCL.CharacterMotion.Settings;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace DCL.CharacterMotion.Animation
{
    public static class ApplyAnimationSlideBlend
    {
        // Going downwards can be also caused by sliding from steep slopes, so the downward animation blends the slide state with the fall blend state
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Execute(float dt,
            ref CharacterAnimationComponent animationComponent,
            in CharacterRigidTransform rigidTransform,
            in IAvatarView view,
            in ICharacterControllerSettings settings)
        {
            int targetSlideBlend = rigidTransform.IsOnASteepSlope ? 1 : 0;

            if (rigidTransform.SteepSlopeAngle < settings.MaxSlopeAngle)
                targetSlideBlend = 0;

            animationComponent.States.SlideBlendValue = Mathf.MoveTowards(animationComponent.States.SlideBlendValue, targetSlideBlend, settings.SlideAnimationBlendSpeed * dt);
            view.SetAnimatorFloat(AnimationHashes.SLIDE_BLEND, animationComponent.States.SlideBlendValue);
        }
    }
}
