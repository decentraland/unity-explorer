using DCL.CharacterMotion.Components;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace DCL.CharacterMotion
{
    public static class ApplyLookDirection
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Execute(CharacterRigidTransform rigidTransform, in MovementInputComponent input)
        {
            if (Mathf.Abs(input.Axes.x) > 0 || Mathf.Abs(input.Axes.y) > 0)
            {
                Vector3 flatVelocity = rigidTransform.MoveVelocity.Velocity;
                flatVelocity.y = 0;
                rigidTransform.LookDirection = flatVelocity.normalized;
            }

            if (rigidTransform.IsOnASteepSlope)
            {
                Vector3 gravityDirection = rigidTransform.GravityDirection;
                gravityDirection.y = 0;
                rigidTransform.LookDirection = gravityDirection.normalized;
            }
        }
    }
}
