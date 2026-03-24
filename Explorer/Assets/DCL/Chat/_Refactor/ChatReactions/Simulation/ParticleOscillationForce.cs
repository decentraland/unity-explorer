using UnityEngine;
using UnityEngine.Profiling;

namespace DCL.Chat.ChatReactions
{
    /// <summary>
    /// Applies sinusoidal lateral oscillation (zig-zag) to particle velocities.
    /// Each particle oscillates in a random horizontal direction based on its phase.
    /// </summary>
    public static class ParticleOscillationForce
    {
        private const float TWO_PI = Mathf.PI * 2f;

        public static void Apply(ChatReactionsParticle[] buffer, int count, float amplitude, float frequency, float dt)
        {
            if (amplitude <= 0f) return;

            Profiler.BeginSample("ChatReactions.World.ZigZag");

            for (int i = 0; i < count; i++)
            {
                ref var p = ref buffer[i];

                float oscillation = Mathf.Sin(p.age * frequency * TWO_PI + p.zigZagPhase)
                                  * amplitude;

                p.vel.x += Mathf.Cos(p.zigZagPhase) * oscillation * dt;
                p.vel.z += Mathf.Sin(p.zigZagPhase) * oscillation * dt;
            }

            Profiler.EndSample();
        }
    }
}
