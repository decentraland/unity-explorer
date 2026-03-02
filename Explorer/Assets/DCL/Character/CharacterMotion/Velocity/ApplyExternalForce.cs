using DCL.CharacterMotion.Components;
using DCL.CharacterMotion.Settings;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace DCL.Character.CharacterMotion
{
    public static class ApplyExternalForce
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Execute(ICharacterControllerSettings settings, ref CharacterRigidTransform characterPhysics, float dt)
        {
            if (characterPhysics.ExternalForce.sqrMagnitude < float.Epsilon)
            {
                characterPhysics.ExternalAcceleration = Vector3.zero;
                characterPhysics.ExternalForce = Vector3.zero;
                return;
            }

            // Vertical acceleration is read by ApplyGravity via ExternalAcceleration.y
            characterPhysics.ExternalAcceleration = characterPhysics.ExternalForce / settings.CharacterMass; // a = F / m
            characterPhysics.ExternalVelocity += characterPhysics.ExternalAcceleration * dt; // v += a * dt

            // Vertical component is handled by the gravity channel — keep ExternalVelocity horizontal-only
            characterPhysics.ExternalVelocity.y = 0f;

            characterPhysics.ExternalForce = Vector3.zero;
        }
    }
}
