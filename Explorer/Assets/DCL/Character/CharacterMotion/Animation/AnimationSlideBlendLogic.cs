using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.Character.CharacterMotion.Components;
using DCL.CharacterMotion.Components;
using DCL.CharacterMotion.Settings;
using DCL.Utilities.Extensions;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace DCL.CharacterMotion.Animation
{
    public static class AnimationSlideBlendLogic
    {
        // Going downwards can be also caused by sliding from steep slopes, so the downward animation blends the slide state with the fall blend state
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsSliding(in CharacterRigidTransform rigidTransform, in ICharacterControllerSettings settings)
        {
            bool targetSlideBlend = rigidTransform.IsOnASteepSlope;

            if (rigidTransform.SteepSlopeAngle < settings.MaxSlopeAngle)
                targetSlideBlend = false;

            return targetSlideBlend;
        }

        // Going downwards can be also caused by sliding from steep slopes, so the downward animation blends the slide state with the fall blend state
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetAnimatorParameters(ref CharacterAnimationComponent animationComponent, IAvatarView view)
        {
            view.SetAnimatorFloat(AnimationHashes.SLIDE_BLEND, animationComponent.States.SlideBlendValue);
        }

        public static float CalculateBlendValue(float dt, float slideBlendValue, bool isSliding, ICharacterControllerSettings settings)
        {
            float targetSlideBlend = isSliding ? 1f : 0f;

            return
                Mathf.MoveTowards(slideBlendValue, targetSlideBlend, settings.SlideAnimationBlendSpeed * dt)
                     .ClampSmallValuesToZero(AnimationMovementBlendLogic.BLEND_EPSILON);
        }
    }
}
