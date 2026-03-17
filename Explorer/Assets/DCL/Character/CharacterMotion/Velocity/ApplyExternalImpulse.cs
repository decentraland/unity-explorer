using DCL.CharacterMotion.Components;
using DCL.CharacterMotion.Settings;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace DCL.Character.CharacterMotion
{
    public static class ApplyExternalImpulse
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Execute(ICharacterControllerSettings settings, ref CharacterRigidTransform characterPhysics)
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
                characterPhysics.IsGrounded = false;

                // fix for jump pads - so that impulse can win (note: gravity velocity can be positive by jump)
                if (characterPhysics.GravityVelocity.y < 0)
                    characterPhysics.GravityVelocity.y = 0;
            }

            characterPhysics.ExternalImpulse = Vector3.zero;
        }
    }
}
