using DCL.CharacterMotion.Components;
using DCL.CharacterMotion.Settings;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace DCL.Character.CharacterMotion.Velocity
{
    public static class ApplyConditionalRotation
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Execute(ref CharacterRigidTransform rigidTransform, in ICharacterControllerSettings settings)
        {
            if (rigidTransform.IsStuck)
                return;

            bool isTimeToRotate = rigidTransform.SteepSlopeTime >= settings.SlopeCharacterRotationDelay;
            bool angleIsTooSteep = rigidTransform.SteepSlopeAngle >= settings.MaxSlopeAngle;

            if (!isTimeToRotate || angleIsTooSteep)
                return;

            Vector3 gravityDirection = rigidTransform.GravityDirection;
            gravityDirection.y = 0;
            rigidTransform.LookDirection = gravityDirection.normalized;
        }
    }
}
