using DCL.Chat.ChatReactions.Configs;
using UnityEngine;

namespace DCL.Chat.ChatReactions
{
    /// <summary>
    /// Stateless helper that computes emoji particle trajectories from
    /// <see cref="ChatReactionFlightPathConfig"/>.
    /// All values are in 2D camera-local space: X = camera right, Y = camera up.
    /// The caller is responsible for converting to world space using the camera basis vectors.
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
        /// Returns the initial 2D spawn velocity for a single particle (camera-local units/sec).
        /// X = camera-right (exit kick away from panel), Y = camera-up (base upward component).
        /// All randomness is resolved here so the particle's trajectory is fixed at birth.
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
        /// Returns the additional 2D acceleration to apply each frame (camera-local units/sec²).
        /// Currently a constant upward float force; extend with curves here if needed.
        /// </summary>
        public Vector2 GetSteering2D(float normalizedAge) =>
            new Vector2(0f, config.FloatUpAcceleration);

        /// <summary>
        /// Evaluates the <see cref="ChatReactionFlightPathConfig.SizeOverLifetime"/> curve
        /// to produce a size multiplier for the pop effect.
        /// </summary>
        public float GetSizeMultiplier(float normalizedAge) =>
            config.SizeOverLifetime.Evaluate(normalizedAge);

        private float Rand(float min, float max) =>
            (float)(min + rng.NextDouble() * (max - min));
    }
}
