using DCL.Chat.ChatReactions.Configs;
using UnityEngine;

namespace DCL.Chat.ChatReactions
{
    /// <summary>
    /// Stateless helper that computes emoji particle trajectories from
    /// <see cref="ChatReactionFlightPathConfig"/>.
    /// All values are in screen space: pixels/sec for velocity, pixels/sec² for acceleration.
    /// X = screen right, Y = screen up.
    /// </summary>
    public sealed class ChatReactionFlightController
    {
        private readonly ChatReactionFlightPathConfig config;
        private readonly System.Random rng;

        public ChatReactionFlightController(ChatReactionFlightPathConfig config, System.Random rng)
        {
            this.config = config;
            this.rng = rng;
        }

        /// <summary>
        /// Returns the initial 2D spawn velocity for a single particle (pixels/sec).
        /// X = screen-right (exit kick away from panel), Y = screen-up (base upward component).
        /// </summary>
        /// <param name="baseUpSpeed">The upward speed drawn from <c>SpeedRange</c> for this particle.</param>
        public Vector2 GetSpawnVelocity2D(float baseUpSpeed)
        {
            float kickMag = Rand(config.ExitKickRange.x, config.ExitKickRange.y);
            float angleDeg = Rand(-config.ExitAngleVarianceDeg, config.ExitAngleVarianceDeg);
            float angleRad = angleDeg * Mathf.Deg2Rad;

            // Rotate the pure-right exit vector by the variance angle
            float kickX = kickMag * Mathf.Cos(angleRad);
            float kickY = kickMag * Mathf.Sin(angleRad);

            float drift = Rand(-config.FloatDriftRange, config.FloatDriftRange);

            return new Vector2(kickX + drift, baseUpSpeed + kickY);
        }

        /// <summary>
        /// Returns the sustained 2D acceleration applied each frame (pixels/sec²).
        /// </summary>
        public Vector2 GetSteering2D() =>
            new(0f, config.FloatUpAcceleration);

        private float Rand(float min, float max) =>
            (float)(min + rng.NextDouble() * (max - min));
    }
}
