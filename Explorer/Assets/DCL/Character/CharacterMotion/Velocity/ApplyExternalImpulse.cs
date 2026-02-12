using DCL.CharacterMotion.Components;
using DCL.CharacterMotion.Settings;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace DCL.CharacterMotion
{
    public static class ApplyExternalImpulse
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Execute(
            ICharacterControllerSettings settings,
            ref CharacterRigidTransform characterPhysics)
        {
            if (characterPhysics.ExternalImpulse.sqrMagnitude < float.Epsilon)
            {
                characterPhysics.ExternalImpulse = Vector3.zero;
                return;
            }

            // Δv = J / m (instant velocity change)
            Vector3 deltaVelocity = characterPhysics.ExternalImpulse / settings.CharacterMass;

            characterPhysics.ExternalVelocity += deltaVelocity;

            // Clamp to max external velocity
            if (characterPhysics.ExternalVelocity.sqrMagnitude > settings.MaxExternalVelocity * settings.MaxExternalVelocity)
                characterPhysics.ExternalVelocity = characterPhysics.ExternalVelocity.normalized * settings.MaxExternalVelocity;

            // Unground if impulse has upward component
            if (characterPhysics.ExternalImpulse.y > 0)
                characterPhysics.IsGrounded = false;

            // Clear impulse accumulator for next frame
            characterPhysics.ExternalImpulse = Vector3.zero;
        }
    }
}
