using DCL.Chat.ChatReactions.Configs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Profiling;

namespace DCL.Chat.ChatReactions.Simulation.World
{
    /// <summary>
    /// Damped spring that pulls anchored particles toward their avatar's XZ position.
    /// Y is left free so particles float upward naturally.
    /// Damping is applied only to the radial velocity component (toward/away from the
    /// anchor) so that tangential motion (zig-zag wobble) is not suppressed.
    /// Damping coefficient: c = dampingRatio * 2 * sqrt(k).
    /// </summary>
    public sealed class AnchorSpringForce : IWorldParticleForce
    {
        private const int LUT_RESOLUTION = 256;

        // Maximum stable impulse factor for explicit-Euler integration.
        // strength*dt >= 1.0 causes divergence; 0.8 provides a safe margin.
        private const float MAX_STABILITY_STEP = 0.8f;

        private readonly AvatarAnchorTable anchors;
        private readonly ChatReactionsWorldLaneConfig config;
        private readonly float[]? curveLut;

        public AnchorSpringForce(AvatarAnchorTable anchors, ChatReactionsWorldLaneConfig config)
        {
            this.anchors = anchors;
            this.config = config;
            this.curveLut = MathUtils.BakeCurve(config.SpringOverLifetime, LUT_RESOLUTION);
        }

        public void Apply(ChatReactionsParticle[] buffer, int count, float dt)
        {
            float strength = config.SpringStrength;
            if (strength <= 0f) return;

            Profiler.BeginSample("ChatReactions.World.AnchorSpring");

            float damping = config.SpringDampingRatio * 2f * Mathf.Sqrt(strength);
            bool hasCurve = curveLut != null;

            for (int i = 0; i < count; i++)
            {
                ref var p = ref buffer[i];
                if (p.anchorIndex == ChatReactionsParticle.ANCHOR_NONE) continue;
                if (!anchors.IsActive(p.anchorIndex)) continue;

                Vector3 anchor = anchors.GetPosition(p.anchorIndex);

                float t = p.lifetime > 0f ? p.age / p.lifetime : 0f;
                
                float curveMultiplier = 1f;

                if (hasCurve)
                {
                    int lutIndex = Mathf.Min((int)(t * (LUT_RESOLUTION - 1)), LUT_RESOLUTION - 1);
                    curveMultiplier = curveLut![lutIndex];
                }

                float effectiveStrength = strength * curveMultiplier;

                // Scaling damping by the same curve means effective damping ratio
                // = configured x sqrt(curveMultiplier) — old particles become slightly
                // underdamped, letting them detach and drift rather than snap back.
                float effectiveDamping = damping * curveMultiplier;

                // Clamp spring impulse to prevent explicit-Euler divergence at low
                // frame rates or hitches. strength*dt >= 1 causes instability.
                float springDt = Mathf.Min(effectiveStrength * dt, MAX_STABILITY_STEP);
                float dampDt = Mathf.Min(effectiveDamping * dt, MAX_STABILITY_STEP);

                float dx = anchor.x - p.pos.x;
                float dz = anchor.z - p.pos.z;
                float distSqr = dx * dx + dz * dz;

                float restoreX = dx * springDt;
                float restoreZ = dz * springDt;

                if (distSqr > 1e-8f)
                {
                    float invDist = math.rsqrt(distSqr);
                    float nx = dx * invDist;
                    float nz = dz * invDist;

                    // Damp only the radial velocity component (toward/away from anchor)
                    // so tangential motion (zig-zag, wobble) is preserved.
                    float radialVel = p.vel.x * nx + p.vel.z * nz;
                    float dampImpulse = radialVel * dampDt;
                    p.vel.x += restoreX - nx * dampImpulse;
                    p.vel.z += restoreZ - nz * dampImpulse;
                }
                else
                {
                    p.vel.x += restoreX;
                    p.vel.z += restoreZ;
                }
            }

            Profiler.EndSample();
        }
    }
}
