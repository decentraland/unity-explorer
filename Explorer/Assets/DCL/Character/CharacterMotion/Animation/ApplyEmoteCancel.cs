using DCL.CharacterMotion.Components;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace DCL.CharacterMotion.Animation
{
    public static class ApplyEmoteCancel
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Execute(ref CharacterAnimationComponent animationComponent, in CharacterRigidTransform rigidTransform)
        {
            if (rigidTransform.MoveVelocity.Velocity.sqrMagnitude > 0.1f || Mathf.Abs(rigidTransform.GravityVelocity.sqrMagnitude) > 0.2f)
                animationComponent.States.IsEmote = false;
        }
    }
}
