using DCL.CharacterMotion.Components;
using DCL.CharacterMotion.Settings;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace DCL.Character.CharacterMotion
{
    public static class ApplyExternalForce
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Execute(ICharacterControllerSettings settings, ref CharacterRigidTransform characterPhysics, in GlideState glideState, float dt)
        {
            // accumulate external forces from all scenes (current and potable experiences)
            characterPhysics.ExternalForce = Vector3.zero;
            foreach (Vector3 contribution in characterPhysics.ExternalForceContributions.Values)
                characterPhysics.ExternalForce += contribution;

            if (characterPhysics.ExternalForce.sqrMagnitude < float.Epsilon)
            {
                characterPhysics.ExternalAcceleration = Vector3.zero;
                return;
            }

            // a = F / m
            characterPhysics.ExternalAcceleration = characterPhysics.ExternalForce / settings.CharacterMass;

            // An open glider catches the airflow with a larger effective area, so continuous external forces act on it stronger
            if (glideState.Value == GlideStateValue.GLIDING)
                characterPhysics.ExternalAcceleration *= settings.GlideWindResponse;

            // v += a * dt (Vertical acceleration is read by ApplyGravity via ExternalAcceleration.y)
            characterPhysics.ExternalVelocity.x += characterPhysics.ExternalAcceleration.x * dt;
            characterPhysics.ExternalVelocity.z += characterPhysics.ExternalAcceleration.z * dt;
        }
    }
}
