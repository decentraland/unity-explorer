using UnityEngine;

namespace DCL.Chat.ChatReactions
{
    /// <summary>
    /// Tracks hold-to-emit streaming state and returns the number of burst ticks
    /// that should fire each frame. Stateless with respect to particle data —
    /// the caller is responsible for actually spawning the particles.
    /// </summary>
    public sealed class UIReactionStreamEmitter
    {
        private float accumulator;

        public bool IsStreaming { get; private set; }
        public RectTransform? Source { get; private set; }

        public void Begin(RectTransform source)
        {
            IsStreaming = true;
            Source = source;
            accumulator = 1f; // fire immediately on the first frame
        }

        public void End()
        {
            IsStreaming = false;
            Source = null;
            accumulator = 0f;
        }

        public void Toggle(RectTransform source)
        {
            if (IsStreaming) End();
            else Begin(source);
        }

        /// <summary>
        /// Advances the accumulator and returns how many burst ticks fired this frame.
        /// Returns 0 if streaming is inactive.
        /// </summary>
        public int Tick(float dt, float ratePerSecond)
        {
            if (!IsStreaming || Source == null) return 0;

            accumulator += dt * ratePerSecond;

            int ticks = 0;

            while (accumulator >= 1f)
            {
                accumulator -= 1f;
                ticks++;
            }

            return ticks;
        }
    }
}
