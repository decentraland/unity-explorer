using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.Character.CharacterMotion.Components;
using DCL.CharacterMotion.Components;
using DCL.CharacterMotion.Settings;
using DCL.CharacterMotion.Utils;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace DCL.CharacterMotion.Animation
{
    public static class ApplyAnimationMovementBlend
    {
        public const float BLEND_EPSILON = 0.01f;

        // The animation state is completely decoupled from the actual velocity, it feels much nicer and has no weird fluctuations
        // state idle ----- walk ----- jog ----- run
        // blend  0  -----   1  -----  2  -----  3
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Execute(
            float dt,
            ref CharacterAnimationComponent animationComponent,
            in ICharacterControllerSettings settings,
            Vector3 velocity,
            bool isGrounded,
            in MovementKind movementKind,
            in IAvatarView view)
        {
            int movementBlendId;
            (movementBlendId, animationComponent.States.MovementBlendValue) =
                UpdateBlendValues(dt, velocity, movementKind, animationComponent.States.MovementBlendValue, settings);

            // we avoid updating the animator value when not grounded to avoid changing the blend tree states based on our speed
            if (!isGrounded)
                return;

            view.SetAnimatorInt(AnimationHashes.MOVEMENT_TYPE, movementBlendId);
            view.SetAnimatorFloat(AnimationHashes.MOVEMENT_BLEND, animationComponent.States.MovementBlendValue);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static (int blendId, float blendValue) UpdateBlendValues(float dt, Vector3 velocity, MovementKind movementKind, float currentMovementBlend,
            in ICharacterControllerSettings settings)
        {
            int movementBlendId = GetMovementBlendId(velocity.sqrMagnitude, movementKind);
            return (movementBlendId, CalculateMovementBlend(dt, currentMovementBlend, movementBlendId, movementKind, velocity.magnitude, settings));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int GetMovementBlendId(float velocitySqrMagnitude, MovementKind speedState)
        {
            if (velocitySqrMagnitude <= 0)
                return 0;

            return speedState switch
                   {
                       MovementKind.WALK => 1,
                       MovementKind.JOG => 2,
                       MovementKind.RUN => 3,
                       _ => 0,
                   };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float CalculateMovementBlend(float dt, float currentMovementBlend, int movementBlendId, MovementKind movementKind, float velocityMagnitude,
            in ICharacterControllerSettings settings)
        {
            float maxVelocity = SpeedLimit.Get(settings, movementKind);
            var targetBlend = 0f;

            if (maxVelocity > 0)
                targetBlend = velocityMagnitude / maxVelocity * movementBlendId;

            float result = Mathf.MoveTowards(currentMovementBlend, targetBlend, dt * settings.MovAnimBlendSpeed);

            return result > BLEND_EPSILON ? result : 0f;
        }
    }
}
