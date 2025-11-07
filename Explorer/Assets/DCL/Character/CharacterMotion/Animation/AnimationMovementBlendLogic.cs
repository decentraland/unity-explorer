using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.Character.CharacterMotion.Components;
using DCL.CharacterMotion.Components;
using DCL.CharacterMotion.Settings;
using DCL.CharacterMotion.Utils;
using DCL.Utilities.Extensions;
using System.Runtime.CompilerServices;
using UnityEngine;
using Utility.Animations;

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
        public static float CalculateBlendValue(float dt, float currentMovementBlend, MovementKind movementKind, float velocitySqrMagnitude, ICharacterControllerSettings settings)
        {
            float animationBlendingSpeedLimit = SpeedLimit.GetAnimationBlendingSpeedLimit(settings, movementKind);
            float targetBlend = 0f;

            if (animationBlendingSpeedLimit > 0)
                targetBlend = Mathf.Sqrt(velocitySqrMagnitude) / animationBlendingSpeedLimit * (int)movementKind;

            // Make blend speed proportional to current delta, similar to a lerp
            // We do this because scenes can override the movement speed and blend speed must react to that
            float blendSpeed = Mathf.Abs(currentMovementBlend - targetBlend) * settings.MovAnimBlendSpeed;
            blendSpeed = Mathf.Max(blendSpeed, settings.MovAnimBlendSpeed);
            float result = Mathf.MoveTowards(currentMovementBlend, targetBlend, dt * blendSpeed);

            return result.ClampSmallValuesToZero(BLEND_EPSILON);
        }
    }
}
