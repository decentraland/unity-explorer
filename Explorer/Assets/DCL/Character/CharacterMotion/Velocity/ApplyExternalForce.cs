using DCL.CharacterMotion.Components;
using DCL.CharacterMotion.Settings;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace DCL.CharacterMotion
{
    public static class ApplyExternalForce
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Execute(
            ICharacterControllerSettings settings,
            ref CharacterRigidTransform characterPhysics,
            float deltaTime)
        {
            if (characterPhysics.ExternalForce.sqrMagnitude < float.Epsilon)
            {
                characterPhysics.ExternalAcceleration = Vector3.zero;
                characterPhysics.ExternalForce = Vector3.zero;
                return;
            }

            // a = F / m
            characterPhysics.ExternalAcceleration = characterPhysics.ExternalForce / settings.CharacterMass;

            // v += a * dt
            characterPhysics.ExternalVelocity += characterPhysics.ExternalAcceleration * deltaTime;

            // Clamp to max external velocity
            if (characterPhysics.ExternalVelocity.sqrMagnitude > settings.MaxExternalVelocity * settings.MaxExternalVelocity)
                characterPhysics.ExternalVelocity = characterPhysics.ExternalVelocity.normalized * settings.MaxExternalVelocity;

            // Unground if force has upward component
            if (characterPhysics.ExternalForce.y > 0)
                characterPhysics.IsGrounded = false;

            // Clear force accumulator for next frame
            characterPhysics.ExternalForce = Vector3.zero;
        }
    }
}
