using UnityEngine;

namespace SuperScrollView
{
    /// <summary>
    /// A short eased move between two leading-space scroll positions, advanced
    /// one step per frame by the view that owns it.
    /// </summary>
    internal struct SnapTween
    {
        private const float DURATION = 0.16f;

        private float from;
        private float to;
        private float elapsed;

        public bool Active { get; private set; }

        public float Target => to;

        public void Begin(float fromLeading, float toLeading)
        {
            from = fromLeading;
            to = toLeading;
            elapsed = 0f;
            Active = true;
        }

        public void Cancel()
        {
            Active = false;
        }

        /// <summary>
        /// Advances by <paramref name="deltaTime"/> and returns the position for this
        /// frame. The tween deactivates on the step that reaches the target.
        /// </summary>
        public float Step(float deltaTime)
        {
            elapsed += deltaTime;
            float t = Mathf.Clamp01(elapsed / DURATION);

            if (t >= 1f)
                Active = false;

            // Smoothstep, so the move leaves and arrives without a velocity step.
            return Mathf.Lerp(from, to, t * t * (3f - (2f * t)));
        }
    }
}
