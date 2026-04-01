using DCL.Chat.ChatReactions.Configs;
using DCL.Chat.ChatReactions.Core;
using UnityEngine;

namespace DCL.Chat.ChatReactions.Simulation.UI
{
    /// <summary>
    /// Stateless helper that computes balloon-style emoji particle trajectories.
    /// Particles get a horizontal kick at spawn, then float upward with sinusoidal zig-zag.
    /// All values are in screen space: pixels/sec for velocity, pixels/sec² for acceleration.
    /// </summary>
    public sealed class ChatReactionFlightController
    {
        private readonly ChatReactionsUILaneConfig config;
        private readonly System.Random rng;

        public ChatReactionFlightController(ChatReactionsUILaneConfig config, System.Random rng)
        {
            this.config = config;
            this.rng = rng;
        }

        /// <summary>
        /// Returns the initial 2D spawn velocity (pixels/sec).
        /// X = horizontal kick (screen-right), Y = gentle upward from InitialUpRange.
        /// </summary>
        public Vector2 GetSpawnVelocity2D() =>
            new(rng.NextFloat(config.KickSpeedRange.x, config.KickSpeedRange.y),
                rng.NextFloat(config.InitialUpRange.x, config.InitialUpRange.y));

        /// <summary>
        /// Returns per-particle 2D acceleration (pixels/sec²) based on age and zig-zag phase.
        /// X = sinusoidal lateral oscillation, Y = sustained upward buoyancy.
        /// </summary>
        public Vector2 GetSteering2D(float age, float zigZagPhase)
        {
            float zigZag = Mathf.Sin(age * config.ZigZagFrequency * MathConstants.TWO_PI + zigZagPhase)
                         * config.ZigZagAmplitude;

            return new Vector2(zigZag, config.FloatUpAcceleration);
        }

        /// <summary>Returns a random phase in [0, 2π] for zig-zag offset.</summary>
        public float GetRandomPhase() =>
            rng.NextFloat(0f, MathConstants.TWO_PI);
    }
}
