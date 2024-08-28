using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.Character.CharacterMotion.Components;
using DCL.CharacterMotion.Components;
using DCL.CharacterMotion.Settings;
using DCL.CharacterMotion.Utils;
using DCL.Utilities.Extensions;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace DCL.CharacterMotion.Animation
{
    public static class AnimationMovementBlendLogic
    {
        public const float BLEND_EPSILON = 0.01f;

        // The animation state is completely decoupled from the actual velocity, it feels much nicer and has no weird fluctuations
        // state idle ----- walk ----- jog ----- run
        // blend  0  -----   1  -----  2  -----  3
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetAnimatorParameters(ref CharacterAnimationComponent animationComponent, IAvatarView view, bool isGrounded, int movementBlendId)
        {
            // we avoid updating the animator value when not grounded to avoid changing the blend tree states based on our speed
            if (!isGrounded)
                return;

            view.SetAnimatorInt(AnimationHashes.MOVEMENT_TYPE, movementBlendId);
            view.SetAnimatorFloat(AnimationHashes.MOVEMENT_BLEND, animationComponent.States.MovementBlendValue);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float CalculateBlendValue(float dt, float currentMovementBlend, MovementKind movementKind, float velocityMagnitude, ICharacterControllerSettings settings)
        {
            float maxVelocity = SpeedLimit.Get(settings, movementKind);
            var targetBlend = 0f;

            if (maxVelocity > 0)
                targetBlend = velocityMagnitude / maxVelocity * (int)movementKind;

            float result = Mathf.MoveTowards(currentMovementBlend, targetBlend, dt * settings.MovAnimBlendSpeed);

            return result.ClampSmallValuesToZero(BLEND_EPSILON);
        }
    }
}
