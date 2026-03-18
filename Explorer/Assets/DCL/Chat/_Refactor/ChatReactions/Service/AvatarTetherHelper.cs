using UnityEngine;
using UnityEngine.Profiling;

namespace DCL.Chat.ChatReactions
{
    /// <summary>
    /// Applies a damped spring tether that pulls anchored particles toward their
    /// avatar's current XZ position. Y is left free so particles float upward naturally.
    /// Formula: F = (strength × displacement − damping × velocity) × lifetimeCurve.
    /// </summary>
    public static class AvatarTetherHelper
    {
        public static void ApplyTetherForces(
            ChatReactionsParticle[] particles,
            AvatarAnchorTable anchors,
            float strength,
            float damping,
            AnimationCurve? tetherOverLifetime,
            float dt)
        {
            if (strength <= 0f) return;

            Profiler.BeginSample("ChatReactions.World.Tether");

            bool hasCurve = tetherOverLifetime != null && tetherOverLifetime.length > 0;

            for (int i = 0; i < particles.Length; i++)
            {
                ref var p = ref particles[i];
                if (p.alive == 0) continue;
                if (p.anchorIndex == ChatReactionsParticle.ANCHOR_NONE) continue;
                if (!anchors.IsActive(p.anchorIndex)) continue;

                Vector3 anchor = anchors.GetPosition(p.anchorIndex);

                float t = p.lifetime > 0f ? p.age / p.lifetime : 0f;
                float curveMultiplier = hasCurve ? tetherOverLifetime!.Evaluate(t) : 1f;
                float effectiveStrength = strength * curveMultiplier;
                float effectiveDamping = damping * curveMultiplier;

                // Damped spring on XZ only — Y floats freely.
                float dx = anchor.x - p.pos.x;
                float dz = anchor.z - p.pos.z;

                p.vel.x += (dx * effectiveStrength - p.vel.x * effectiveDamping) * dt;
                p.vel.z += (dz * effectiveStrength - p.vel.z * effectiveDamping) * dt;
            }

            Profiler.EndSample();
        }
    }
}
