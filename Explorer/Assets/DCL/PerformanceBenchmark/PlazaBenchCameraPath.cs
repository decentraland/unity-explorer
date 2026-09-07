using UnityEngine;

namespace DCL.PerformanceBenchmark
{
    /// <summary>
    ///     A camera pose on the benchmark path: where the camera sits and the world point it aims at.
    /// </summary>
    public readonly struct PlazaBenchPose
    {
        public readonly Vector3 Position;
        public readonly Vector3 LookAt;

        public PlazaBenchPose(Vector3 position, Vector3 lookAt)
        {
            Position = position;
            LookAt = lookAt;
        }
    }

    /// <summary>
    ///     Pure evaluation of the deterministic plaza-recording camera path. The path is parameterized
    ///     exclusively by elapsed path time (frame-rate independent) and anchored at the player spawn
    ///     position: 15 s standing look-out (P1), 45 s slow 360 degree orbit (P2 = orbit midpoint),
    ///     15 s pull-back looking down at the plaza (P3).
    /// </summary>
    public static class PlazaBenchCameraPath
    {
        public const float STAND_SECONDS = 15f;
        public const float ORBIT_SECONDS = 45f;
        public const float PULL_BACK_SECONDS = 15f;
        public const float TOTAL_SECONDS = STAND_SECONDS + ORBIT_SECONDS + PULL_BACK_SECONDS;

        /// <summary>Path times of the three golden-capture poses. P1 mid-stand, P2 orbit midpoint, P3 settled pull-back.</summary>
        public const float POSE_P1_TIME = STAND_SECONDS * 0.5f;
        public const float POSE_P2_TIME = STAND_SECONDS + (ORBIT_SECONDS * 0.5f);
        public const float POSE_P3_TIME = TOTAL_SECONDS - 2.5f;

        private const float EYE_HEIGHT = 1.8f;
        private const float LOOK_OUT_DISTANCE = 50f;
        private const float ORBIT_RADIUS = 14f;
        private const float ORBIT_HEIGHT = 6f;
        private const float ORBIT_LOOK_HEIGHT = 2f;
        private const float PULL_BACK_BLEND_SECONDS = 5f;
        private static readonly Vector3 PULL_BACK_OFFSET = new (0f, 30f, -35f);

        /// <summary>
        ///     Evaluates the pose at <paramref name="pathTime" /> seconds (clamped to [0, TOTAL_SECONDS])
        ///     around the <paramref name="anchor" /> world position (the player spawn point).
        /// </summary>
        public static PlazaBenchPose Evaluate(Vector3 anchor, float pathTime)
        {
            float t = Mathf.Clamp(pathTime, 0f, TOTAL_SECONDS);

            if (t < STAND_SECONDS)
            {
                Vector3 position = anchor + (Vector3.up * EYE_HEIGHT);
                return new PlazaBenchPose(position, position + (Vector3.forward * LOOK_OUT_DISTANCE));
            }

            if (t < STAND_SECONDS + ORBIT_SECONDS)
            {
                float theta = 2f * Mathf.PI * ((t - STAND_SECONDS) / ORBIT_SECONDS);
                Vector3 position = anchor + new Vector3(ORBIT_RADIUS * Mathf.Sin(theta), ORBIT_HEIGHT, ORBIT_RADIUS * Mathf.Cos(theta));
                return new PlazaBenchPose(position, anchor + (Vector3.up * ORBIT_LOOK_HEIGHT));
            }

            // Pull-back: blend from the orbit end pose to a high look-down pose, then hold.
            float blend = Mathf.SmoothStep(0f, 1f, (t - STAND_SECONDS - ORBIT_SECONDS) / PULL_BACK_BLEND_SECONDS);
            Vector3 orbitEndPosition = anchor + new Vector3(0f, ORBIT_HEIGHT, ORBIT_RADIUS);
            Vector3 pullBackPosition = Vector3.Lerp(orbitEndPosition, anchor + PULL_BACK_OFFSET, blend);
            Vector3 lookAt = Vector3.Lerp(anchor + (Vector3.up * ORBIT_LOOK_HEIGHT), anchor, blend);
            return new PlazaBenchPose(pullBackPosition, lookAt);
        }
    }
}
