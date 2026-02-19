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

            // Clamp horizontal external velocity
            ClampHorizontally(settings, characterPhysics);

            characterPhysics.ExternalForce = Vector3.zero;
        }

        private static void ClampHorizontally(ICharacterControllerSettings settings, CharacterRigidTransform characterPhysics)
        {
            float hSqr = (characterPhysics.ExternalVelocity.x * characterPhysics.ExternalVelocity.x)
                         + (characterPhysics.ExternalVelocity.z * characterPhysics.ExternalVelocity.z);

            float maxSqr = settings.MaxExternalVelocity * settings.MaxExternalVelocity;
            if (hSqr > maxSqr)
            {
                float scale = settings.MaxExternalVelocity / Mathf.Sqrt(hSqr);
                characterPhysics.ExternalVelocity.x *= scale;
                characterPhysics.ExternalVelocity.z *= scale;
            }
        }
    }
}
