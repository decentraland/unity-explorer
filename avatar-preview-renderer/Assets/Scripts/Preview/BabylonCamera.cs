using UnityEngine;

namespace Preview
{
    /// <summary>
    /// The Babylon preview's orbit camera, so its <c>cameraX</c>, <c>cameraY</c> and <c>cameraZ</c>
    /// options can move this renderer's fitted views. Those views already stand where Babylon's
    /// default cameras do, so only the difference from the default is applied: a turn, a tilt and a
    /// change of distance.
    /// </summary>
    public static class BabylonCamera
    {
        /// <summary>
        /// Babylon's camera for an avatar, in its world.
        /// </summary>
        public static readonly Vector3 AVATAR = new(0f, 1f, 3.5f);

        /// <summary>
        /// Babylon's camera for an item on its own.
        /// </summary>
        public static readonly Vector3 ITEM = new(-2f, 2f, 2f);

        /// <summary>
        /// The camera the page asked for: every axis it left unset keeps the default's.
        /// </summary>
        public static Vector3 Resolve(PreviewConfiguration config, Vector3 fallback) =>
            new(config.CameraX ?? fallback.x, config.CameraY ?? fallback.y, config.CameraZ ?? fallback.z);

        /// <summary>
        /// How much larger the subject shows from <paramref name="position"/> than from
        /// <paramref name="fallback"/>: the ratio of their distances to the target.
        /// </summary>
        public static float Closer(Vector3 position, Vector3 fallback)
        {
            var distance = position.magnitude;
            return distance > Mathf.Epsilon ? fallback.magnitude / distance : 1f;
        }

        /// <summary>
        /// The turn and tilt, in degrees, from <paramref name="fallback"/> to <paramref name="position"/>,
        /// as Babylon's alpha (around the vertical) and beta (down from the top) change, in the order
        /// the page's orbit deltas take them.
        /// </summary>
        public static (float alpha, float beta) AnglesFrom(Vector3 position, Vector3 fallback)
        {
            var (alpha, beta) = Angles(position);
            var (fallbackAlpha, fallbackBeta) = Angles(fallback);

            return (Mathf.DeltaAngle(fallbackAlpha, alpha), beta - fallbackBeta);
        }

        private static (float alpha, float beta) Angles(Vector3 position)
        {
            var distance = position.magnitude;
            if (distance <= Mathf.Epsilon) return (0f, 0f);

            return (Mathf.Atan2(position.z, position.x) * Mathf.Rad2Deg,
                Mathf.Acos(Mathf.Clamp(position.y / distance, -1f, 1f)) * Mathf.Rad2Deg);
        }
    }
}
