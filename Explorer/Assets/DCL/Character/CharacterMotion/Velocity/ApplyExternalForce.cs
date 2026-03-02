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

            characterPhysics.ExternalAcceleration = characterPhysics.ExternalForce / settings.CharacterMass; // a = F / m

            // v += a * dt (Vertical acceleration is read by ApplyGravity via ExternalAcceleration.y)
            characterPhysics.ExternalVelocity.x += characterPhysics.ExternalAcceleration.x * dt;
            characterPhysics.ExternalVelocity.z += characterPhysics.ExternalAcceleration.z * dt; // v += a * dt

            characterPhysics.ExternalForce = Vector3.zero;
        }
    }
}
