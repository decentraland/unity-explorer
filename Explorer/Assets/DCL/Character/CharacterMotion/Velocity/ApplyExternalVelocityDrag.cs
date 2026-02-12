using DCL.CharacterMotion.Components;
using DCL.CharacterMotion.Settings;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace DCL.CharacterMotion
{
    /// <summary>
    ///     Applies damping to ExternalVelocity using a linear (viscous) drag model.
    ///     <para>
    ///         Physical model: dv/dt = -damping · v (velocity-proportional resistance).
    ///         Discrete form: v *= (1 - damping · dt).
    ///     </para>
    ///     <para>
    ///         This is the standard model used by Unity (Rigidbody.drag), Unreal (LinearDamping),
    ///         Godot (linear_damp), Box2D and Bullet. It guarantees a stable terminal velocity
    ///         when a constant force is applied: v_terminal = F / (m · damping).
    ///     </para>
    ///     <para>
    ///         Total damping = envDrag + groundFriction (when grounded).
    ///         envDrag: environment/medium resistance — always active (atmosphere, water, space).
    ///         groundFriction: surface contact resistance — only when grounded (ice, concrete, mud).
    ///     </para>
    /// </summary>
    public static class ApplyExternalVelocityDrag
    {
        private const float MIN_VELOCITY_THRESHOLD = 0.01f;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Execute(
            ICharacterControllerSettings settings,
            ref CharacterRigidTransform characterPhysics,
            float deltaTime)
        {
            // Reset vertical component when grounded
            if (characterPhysics.IsGrounded && characterPhysics.ExternalVelocity.y < 0)
                characterPhysics.ExternalVelocity.y = 0;

            if (characterPhysics.ExternalVelocity.sqrMagnitude < MIN_VELOCITY_THRESHOLD * MIN_VELOCITY_THRESHOLD)
            {
                characterPhysics.ExternalVelocity = Vector3.zero;
                return;
            }

            // Additive damping: environment is always present, ground contact adds on top
            float damping = settings.ExternalEnvDrag;

            if (characterPhysics.IsGrounded)
                damping += settings.ExternalGroundFriction;

            // Linear (viscous) damping: v *= (1 - damping · dt)
            // Simulates velocity-proportional resistance (like moving through a medium)
            float factor = Mathf.Max(0f, 1f - damping * deltaTime);
            characterPhysics.ExternalVelocity *= factor;

            // Snap to zero when below threshold to avoid asymptotic creep
            if (characterPhysics.ExternalVelocity.sqrMagnitude < MIN_VELOCITY_THRESHOLD * MIN_VELOCITY_THRESHOLD)
                characterPhysics.ExternalVelocity = Vector3.zero;
        }
    }
}
