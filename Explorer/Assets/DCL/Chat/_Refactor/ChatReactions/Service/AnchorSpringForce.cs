using DCL.Chat.ChatReactions.Configs;
using UnityEngine;
using UnityEngine.Profiling;

namespace DCL.Chat.ChatReactions
{
    /// <summary>
    /// Damped spring that pulls anchored particles toward their avatar's XZ position.
    /// Y is left free so particles float upward naturally.
    /// Damping coefficient: c = dampingRatio * 2 * sqrt(k).
    /// </summary>
    public sealed class AnchorSpringForce : IWorldParticleForce
    {
        private readonly AvatarAnchorTable anchors;
        private readonly ChatReactionsWorldLaneConfig config;

        public AnchorSpringForce(AvatarAnchorTable anchors, ChatReactionsWorldLaneConfig config)
        {
            this.anchors = anchors;
            this.config = config;
        }

        public void Apply(ChatReactionsParticle[] buffer, int count, float dt)
        {
            float strength = config.SpringStrength;
            if (strength <= 0f) return;

            Profiler.BeginSample("ChatReactions.World.AnchorSpring");

            float damping = config.SpringDampingRatio * 2f * Mathf.Sqrt(strength);
            AnimationCurve curve = config.SpringOverLifetime;
            bool hasCurve = curve != null && curve.length > 0;

            for (int i = 0; i < count; i++)
            {
                ref var p = ref buffer[i];
                if (p.anchorIndex == ChatReactionsParticle.ANCHOR_NONE) continue;
                if (!anchors.IsActive(p.anchorIndex)) continue;

                Vector3 anchor = anchors.GetPosition(p.anchorIndex);

                float t = p.lifetime > 0f ? p.age / p.lifetime : 0f;
                float curveMultiplier = hasCurve ? curve.Evaluate(t) : 1f;
                float effectiveStrength = strength * curveMultiplier;

                // Scaling damping by the same curve means effective damping ratio
                // = configured × √curveMultiplier — old particles become slightly
                // underdamped, letting them detach and drift rather than snap back.
                float effectiveDamping = damping * curveMultiplier;

                float dx = anchor.x - p.pos.x;
                float dz = anchor.z - p.pos.z;

                p.vel.x += (dx * effectiveStrength - p.vel.x * effectiveDamping) * dt;
                p.vel.z += (dz * effectiveStrength - p.vel.z * effectiveDamping) * dt;
            }

            Profiler.EndSample();
        }
    }
}
