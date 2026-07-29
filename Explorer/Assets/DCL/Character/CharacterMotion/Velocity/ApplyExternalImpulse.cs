using DCL.CharacterMotion.Components;
using DCL.CharacterMotion.Settings;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace DCL.Character.CharacterMotion
{
    public static class ApplyExternalImpulse
    {
        // Ground proximity within which an upward impulse still counts as a landing:
        // the scene can launch the character before our physics registers a grounded tick
        private const float JUMP_RESET_GROUND_DISTANCE = 1f;

        // Minimum time between two jump-counter resets caused by a mid-air descending impulse,
        // so a scene spamming impulses at a falling character can't restore jumps every tick
        private const float DESCENDING_RESET_COOLDOWN = 1f;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Execute(ICharacterControllerSettings settings, ref CharacterRigidTransform characterPhysics, ref JumpState jumpState, int physicsTick, float dt)
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
                bool onOrNearGround = characterPhysics.IsGrounded || characterPhysics.GroundDistance <= JUMP_RESET_GROUND_DISTANCE;
                bool descendingReset = characterPhysics.GravityVelocity.y + characterPhysics.ExternalVelocity.y < 0f &&
                                       (physicsTick - jumpState.LastDescendingResetTick) * dt >= DESCENDING_RESET_COOLDOWN;

                if (onOrNearGround || descendingReset)
                {
                    jumpState.JumpCount = 0;
                    jumpState.AirJumpDelay = float.MinValue;

                    if (descendingReset)
                        jumpState.LastDescendingResetTick = physicsTick;
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
