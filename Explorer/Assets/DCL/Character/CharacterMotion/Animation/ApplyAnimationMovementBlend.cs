using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.CharacterMotion.Components;
using DCL.CharacterMotion.Settings;
using DCL.CharacterMotion.Utils;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace DCL.CharacterMotion.Animation
{
    public static class ApplyAnimationMovementBlend
    {
        // The animation state is completely decoupled from the actual velocity, it feels much nicer and has no weird fluctuations
        // state idle ----- walk ----- jog ----- run
        // blend  0  -----   1  -----  2  -----  3
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Execute(
            float dt,
            ref CharacterAnimationComponent animationComponent,
            in ICharacterControllerSettings settings,
            in CharacterRigidTransform rigidTransform,
            in MovementInputComponent movementInput,
            in IAvatarView view)
        {
            var velocity = rigidTransform.MoveVelocity.Velocity;
            float maxVelocity = SpeedLimit.Get(settings, movementInput.Kind);

            int movementBlendId = GetMovementBlendId(velocity, movementInput.Kind);

            var targetBlend = 0f;

            if (maxVelocity > 0)
                targetBlend = velocity.magnitude / maxVelocity * movementBlendId;

            animationComponent.States.MovementBlendValue = Mathf.MoveTowards(animationComponent.States.MovementBlendValue, targetBlend, dt * settings.MovAnimBlendSpeed);

            // we avoid updating the animator value when not grounded to avoid changing the blend tree states based on our speed
            if (!rigidTransform.IsGrounded)
                return;

            view.SetAnimatorFloat(AnimationHashes.MOVEMENT_BLEND, animationComponent.States.MovementBlendValue);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int GetMovementBlendId(Vector3 velocity, MovementKind speedState)
        {
            if (velocity.sqrMagnitude <= 0)
                return 0;

            return speedState switch
                   {
                       MovementKind.Walk => 1,
                       MovementKind.Jog => 2,
                       MovementKind.Run => 3,
                       _ => 0,
                   };
        }
    }
}
