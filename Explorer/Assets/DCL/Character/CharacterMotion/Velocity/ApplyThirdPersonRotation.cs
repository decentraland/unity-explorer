using DCL.CharacterMotion.Components;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace DCL.Character.CharacterMotion.Velocity
{
    public static class ApplyThirdPersonRotation
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Execute(ref CharacterRigidTransform rigidTransform, in MovementInputComponent input)
        {
            if (!(Mathf.Abs(input.Axes.x) > 0) && !(Mathf.Abs(input.Axes.y) > 0))
                return;

            Vector3 flatVelocity = rigidTransform.MoveVelocity.Velocity;
            flatVelocity.y = 0;
            rigidTransform.LookDirection = flatVelocity.normalized;
        }
    }
}
