using DCL.Character.CharacterMotion.Components;
using DCL.CharacterMotion.Components;
using DCL.CharacterMotion.Settings;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace DCL.CharacterMotion
{
    public static class ApplyImpulse
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Execute(
            ICharacterControllerSettings settings,
            ref CharacterRigidTransform characterPhysics,
            ref ImpulseInputComponent impulseInput)
        {
            if (!impulseInput.WasTriggered)
                return;

            // Calculate impulse velocity: v = F / m (instantaneous velocity change)
            // Direction is already in world space
            Vector3 impulseVelocity = settings.ImpulseDirection.normalized * (settings.ImpulseForce / settings.CharacterMass);

            // Apply impulse to gravity velocity (affects vertical component)
            characterPhysics.GravityVelocity += new Vector3(0, impulseVelocity.y, 0);
            
            // Apply horizontal impulse to move velocity
            characterPhysics.MoveVelocity.Velocity += new Vector3(impulseVelocity.x, 0, impulseVelocity.z);

            // Consume the impulse
            impulseInput.WasTriggered = false;
            
            // Unground the character if impulse has upward component
            if (impulseVelocity.y > 0)
                characterPhysics.IsGrounded = false;
        }
    }
}
