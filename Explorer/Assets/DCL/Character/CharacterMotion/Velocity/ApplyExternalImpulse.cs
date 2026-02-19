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

            // Vertical component decays via drag and is zeroed on landing by ApplyExternalVelocityDrag.
            Vector3 deltaVelocity = characterPhysics.ExternalImpulse / settings.CharacterMass; // Δv = J / m (instant velocity change)
            characterPhysics.ExternalVelocity += deltaVelocity;

            if (characterPhysics.ExternalImpulse.y > 0f)
                characterPhysics.IsGrounded = false;

            // Clamp to max external velocity
            if (characterPhysics.ExternalVelocity.sqrMagnitude > settings.MaxExternalVelocity * settings.MaxExternalVelocity)
                characterPhysics.ExternalVelocity = characterPhysics.ExternalVelocity.normalized * settings.MaxExternalVelocity;

            characterPhysics.ExternalImpulse = Vector3.zero;
        }
    }
}
