using DCL.Character.CharacterMotion.Components;
using DCL.CharacterMotion.Components;
using DCL.CharacterMotion.Settings;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace DCL.CharacterMotion
{
    public static class ApplyImpulse
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Execute(
            ICharacterControllerSettings settings,
            ref CharacterRigidTransform characterPhysics,
            ref ImpulseInputComponent impulseInput)
        {
            if (!impulseInput.WasTriggered)
                return;

            // Calculate impulse velocity: v = F / m (instantaneous velocity change)
            // Direction is already in world space
            Vector3 impulseVelocity = settings.ImpulseDirection.normalized * (settings.ImpulseForce / settings.CharacterMass);

            // Add to external velocity (will decay over time via drag/friction)
            characterPhysics.ExternalVelocity += impulseVelocity;

            // Clamp to max
            if (characterPhysics.ExternalVelocity.sqrMagnitude > settings.MaxExternalVelocity * settings.MaxExternalVelocity)
                characterPhysics.ExternalVelocity = characterPhysics.ExternalVelocity.normalized * settings.MaxExternalVelocity;

            // Consume the impulse
            impulseInput.WasTriggered = false;
            
            // Unground the character if impulse has upward component
            if (impulseVelocity.y > 0)
                characterPhysics.IsGrounded = false;
        }
    }
}
