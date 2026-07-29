using DCL.CharacterMotion.Components;
using DCL.CharacterMotion.Settings;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace DCL.Character.CharacterMotion
{
    public static class ApplyExternalImpulse
    {
        // Ground proximity within which an upward impulse counts as a landing: the scene detects the touch
        // on its own tick and can launch the character before our physics registers a grounded tick
        private const float JUMP_RESET_GROUND_DISTANCE = 0.3f;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Execute(ICharacterControllerSettings settings, ref CharacterRigidTransform characterPhysics, ref JumpState jumpState)
        {
            if (characterPhysics.ExternalImpulse.sqrMagnitude < float.Epsilon)
            {
                characterPhysics.ExternalImpulse = Vector3.zero;
                return;
            }

            Vector3 deltaVelocity = characterPhysics.ExternalImpulse / settings.CharacterMass; // Δv = J / m (instant velocity change)
            characterPhysics.ExternalVelocity += deltaVelocity;

            if (characterPhysics.ExternalImpulse.y > 0f)
            {
                // An upward impulse taken on (or within centimeters of) the ground is a landing:
                // reset the jump counter like ApplyJump does, since after the launch there may be no grounded tick at all
                if (characterPhysics.IsGrounded || characterPhysics.GroundDistance <= JUMP_RESET_GROUND_DISTANCE)
                {
                    jumpState.JumpCount = 0;
                    jumpState.AirJumpDelay = float.MinValue;
                }

                characterPhysics.IsGrounded = false;

                // fix for jump pads - so that impulse can win (note: gravity velocity can be positive by jump)
                if (characterPhysics.GravityVelocity.y < 0)
                    characterPhysics.GravityVelocity.y = 0;
            }

            characterPhysics.ExternalImpulse = Vector3.zero;
        }
    }
}
