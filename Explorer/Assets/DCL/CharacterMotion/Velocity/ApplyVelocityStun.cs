using DCL.CharacterMotion.Components;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace DCL.CharacterMotion
{
    public static class ApplyVelocityStun
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Execute(ref CharacterRigidTransform rigidTransform, in StunComponent stunComponent)
        {
            if (stunComponent.IsStunned)
                rigidTransform.MoveVelocity.Velocity = Vector3.zero;
        }
    }
}
