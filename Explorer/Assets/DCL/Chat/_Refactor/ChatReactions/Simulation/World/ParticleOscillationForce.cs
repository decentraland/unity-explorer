using DCL.Chat.ChatReactions.Configs;
using UnityEngine;
using UnityEngine.Profiling;

namespace DCL.Chat.ChatReactions.Simulation.World
{
    /// <summary>
    /// Sinusoidal lateral oscillation (zig-zag) applied to particle velocities.
    /// Each particle's random phase determines its oscillation direction.
    /// Designed to pair with a radial-only spring so the two forces stay independent.
    ///
    /// NOTE: Currently the zig-zag oscillation is applied as a render-time offset
    /// inside ChatReactionsParticleRenderer rather than as a physics force here.
    /// This class is kept as the intended long-term approach — oscillation should be
    /// separated from rendering and applied as a proper force in the simulation tick,
    /// so that culling positions reflect the actual visual wobble.
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

            float omega = config.ZigZagFrequency * MathConstants.TWO_PI;
            float scaledAmplitude = amplitude * dt;

            for (int i = 0; i < count; i++)
            {
                ref var p = ref buffer[i];

                float oscillation = Mathf.Sin(p.age * omega + p.zigZagPhase) * scaledAmplitude;
                p.vel.x += Mathf.Cos(p.zigZagPhase) * oscillation;
                p.vel.z += Mathf.Sin(p.zigZagPhase) * oscillation;
            }

            Profiler.EndSample();
        }
    }
}
