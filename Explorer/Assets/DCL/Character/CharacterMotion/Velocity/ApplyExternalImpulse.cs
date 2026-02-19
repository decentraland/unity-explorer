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

            // Δv = J / m (instant velocity change)
            Vector3 deltaVelocity = characterPhysics.ExternalImpulse / settings.CharacterMass;

            // Horizontal → ExternalVelocity (decays via drag)
            characterPhysics.ExternalVelocity.x += deltaVelocity.x;
            characterPhysics.ExternalVelocity.z += deltaVelocity.z;
            // Vertical → GravityVelocity (same channel as jump and gravity — single vertical physics model)
            characterPhysics.GravityVelocity.y += deltaVelocity.y;

            if (deltaVelocity.y > 0f)
                characterPhysics.IsGrounded = false;

            ClampHorizontally(settings, characterPhysics);

            characterPhysics.ExternalImpulse = Vector3.zero;
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
