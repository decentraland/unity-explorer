using DCL.Chat.ChatReactions.Configs;
using UnityEngine;
using UnityEngine.Profiling;

namespace DCL.Chat.ChatReactions.Simulation.World
{
    /// <summary>
    /// Exponential follow that smoothly pulls anchored particles toward their avatar's
    /// XZ position. Y is left free so particles float upward naturally.
    /// Frame-rate independent: blend = 1 - exp(-rate * dt).
    /// </summary>
    public sealed class AnchorFollowForce : IWorldParticleForce
    {
        private const int LUT_RESOLUTION = 256;

        private readonly AvatarAnchorTable anchors;
        private readonly ChatReactionsWorldLaneConfig config;
        private readonly float[]? curveLut;

        public AnchorFollowForce(AvatarAnchorTable anchors, ChatReactionsWorldLaneConfig config)
        {
            this.anchors = anchors;
            this.config = config;
            this.curveLut = MathUtils.BakeCurve(config.FollowOverLifetime, LUT_RESOLUTION);
        }

        public void Apply(ChatReactionsParticle[] buffer, int count, float dt)
        {
            float rate = config.FollowRate;
            if (rate <= 0f) return;

            Profiler.BeginSample("ChatReactions.World.AnchorFollow");

            bool hasCurve = curveLut != null;

            for (int i = 0; i < count; i++)
            {
                ref var p = ref buffer[i];
                if (p.anchorIndex == ChatReactionsParticle.ANCHOR_NONE) continue;
                if (!anchors.IsActive(p.anchorIndex)) continue;

                Vector3 anchor = anchors.GetPosition(p.anchorIndex);

                float curveMultiplier = 1f;

                if (hasCurve)
                {
                    float t = p.lifetime > 0f ? p.age / p.lifetime : 0f;
                    int lutIndex = Mathf.Min((int)(t * (LUT_RESOLUTION - 1)), LUT_RESOLUTION - 1);
                    curveMultiplier = curveLut![lutIndex];
                }

                float blend = 1f - Mathf.Exp(-rate * curveMultiplier * dt);

                p.pos.x += (anchor.x - p.pos.x) * blend;
                p.pos.z += (anchor.z - p.pos.z) * blend;
            }

            Profiler.EndSample();
        }
    }
}
