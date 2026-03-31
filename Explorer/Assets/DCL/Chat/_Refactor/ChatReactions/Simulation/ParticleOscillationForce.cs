using DCL.Chat.ChatReactions.Configs;
using UnityEngine;
using UnityEngine.Profiling;

namespace DCL.Chat.ChatReactions
{
    /// <summary>
    /// Sinusoidal lateral oscillation (zig-zag) applied to particle velocities.
    /// Each particle's random phase determines its oscillation direction.
    /// </summary>
    public sealed class ParticleOscillationForce : IWorldParticleForce
    {
        private readonly ChatReactionsWorldLaneConfig config;

        public ParticleOscillationForce(ChatReactionsWorldLaneConfig config)
        {
            this.config = config;
        }

        public void Apply(ChatReactionsParticle[] buffer, int count, float dt)
        {
            float amplitude = config.ZigZagAmplitude;
            if (amplitude <= 0f) return;

            Profiler.BeginSample("ChatReactions.World.ZigZag");

            float frequency = config.ZigZagFrequency;

            for (int i = 0; i < count; i++)
            {
                ref var p = ref buffer[i];

                float oscillation = Mathf.Sin(p.age * frequency * MathConstants.TWO_PI + p.zigZagPhase)
                                  * amplitude;

                p.vel.x += Mathf.Cos(p.zigZagPhase) * oscillation * dt;
                p.vel.z += Mathf.Sin(p.zigZagPhase) * oscillation * dt;
            }

            Profiler.EndSample();
        }
    }
}
