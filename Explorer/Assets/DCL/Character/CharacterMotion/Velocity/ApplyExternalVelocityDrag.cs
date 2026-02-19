using DCL.CharacterMotion.Components;
using DCL.CharacterMotion.Settings;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace DCL.Character.CharacterMotion
{
    /// <summary>
    ///     Applies damping to ExternalVelocity using a linear (viscous) drag model.
    ///     Horizontal: from both impulses and continuous forces (forces write only XZ).
    ///     Vertical: from impulses only (continuous vertical forces go through ApplyGravity via effective gravity).
    ///     <para>
    ///         Physical model: dv/dt = - damping · v (velocity-proportional resistance).
    ///         Discrete form: v *= (1 - damping · dt).
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
        public static void Execute(ICharacterControllerSettings settings, ref CharacterRigidTransform characterPhysics, float dt)
        {
            // Additive damping: environment is always present, ground contact adds on top
            float damping = characterPhysics.IsGrounded
                ? settings.ExternalEnvDrag + settings.ExternalGroundFriction
                : settings.ExternalEnvDrag;

            // Linear (viscous) damping: v *= (1 - damping · dt)
            characterPhysics.ExternalVelocity *= Mathf.Max(0f, 1f - (damping * dt));

            // Snap to zero when below a threshold to avoid asymptotic creep
            if (characterPhysics.ExternalVelocity.sqrMagnitude < MIN_VELOCITY_THRESHOLD * MIN_VELOCITY_THRESHOLD)
                characterPhysics.ExternalVelocity = Vector3.zero;

            // Zero vertical component on ground to prevent bounce after landing from an impulse
            if (characterPhysics.IsGrounded)
                characterPhysics.ExternalVelocity.y = 0f;
        }
    }
}
