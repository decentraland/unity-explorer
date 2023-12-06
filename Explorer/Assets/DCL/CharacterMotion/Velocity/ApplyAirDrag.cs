using DCL.CharacterMotion.Components;
using DCL.CharacterMotion.Settings;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace DCL.CharacterMotion
{
    public static class ApplyAirDrag
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Execute(ICharacterControllerSettings characterControllerSettings,
            ref CharacterRigidTransform characterPhysics,
            float deltaTime)
        {
            if (!characterPhysics.IsGrounded || characterPhysics.IsOnASteepSlope)
            {
                var tempVelocity = characterPhysics.MoveVelocity.Velocity;
                tempVelocity.y = 0;
                tempVelocity = ApplyDrag(tempVelocity, characterControllerSettings.AirDrag * characterControllerSettings.JumpVelocityDrag, deltaTime);
                tempVelocity.y = characterPhysics.MoveVelocity.Velocity.y;

                characterPhysics.MoveVelocity.Velocity = tempVelocity;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector3 ApplyDrag(Vector3 vector, float drag, float deltaTime)
        {
            float velocityMagnitude = vector.magnitude;
            float dragMagnitude = drag * velocityMagnitude * velocityMagnitude;
            Vector3 dragDirection = -vector.normalized;
            Vector3 dragForce = dragDirection * dragMagnitude;
            vector += dragForce * deltaTime;
            return vector;
        }
    }
}
