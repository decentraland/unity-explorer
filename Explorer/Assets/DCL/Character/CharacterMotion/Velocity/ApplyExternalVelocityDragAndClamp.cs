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
    public static class ApplyExternalVelocityDragAndClamp
    {
        private const float MIN_SQR_VELOCITY_THRESHOLD = 0.01f * 0.01f;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Execute(ICharacterControllerSettings settings, ref CharacterRigidTransform characterPhysics, float dt)
        {
            // Additive damping: environment is always present, ground contact adds on top
            float damping = characterPhysics.IsGrounded
                ? settings.ExternalEnvDrag + settings.ExternalGroundFriction
                : settings.ExternalEnvDrag;

            // Linear (viscous) damping: v *= (1 - damping · dt)
            characterPhysics.ExternalVelocity *= Mathf.Max(0f, 1f - (damping * dt));

            // Zero vertical component on ground to prevent bounce after landing from an impulse
            if (characterPhysics.IsGrounded)
                characterPhysics.ExternalVelocity.y = 0f;

            characterPhysics.ExternalVelocity = Clamp(characterPhysics.ExternalVelocity.sqrMagnitude, settings, characterPhysics);
        }

        private static Vector3 Clamp(float velocitySqrMagnitude, ICharacterControllerSettings settings, CharacterRigidTransform characterPhysics)
        {
            if (velocitySqrMagnitude < MIN_SQR_VELOCITY_THRESHOLD)
                return Vector3.zero;

            return velocitySqrMagnitude > settings.MaxExternalVelocity * settings.MaxExternalVelocity
                ? characterPhysics.ExternalVelocity.normalized * settings.MaxExternalVelocity
                : characterPhysics.ExternalVelocity;
        }
    }
}
