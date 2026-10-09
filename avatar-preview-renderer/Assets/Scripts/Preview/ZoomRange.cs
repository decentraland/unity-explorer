using UnityEngine;

namespace Preview
{
    /// <summary>
    /// The zoom factor over the fitted view: 1 shows the subject as the fit framed it, 2 shows it
    /// twice as large. The limits mirror the Babylon preview's orbit radius limits, which the apps
    /// drive through <c>zoom</c>, <c>wheelZoom</c> and <c>wheelStart</c>, so one set of options frames
    /// both renderers alike.
    /// </summary>
    public readonly struct ZoomRange
    {
        // Babylon maps zoom 0..100 onto a radius divisor of 1..2.8.
        private const float MIN_ZOOM_FACTOR = 1f;
        private const float MAX_ZOOM_FACTOR = 2.8f;

        // Babylon's default camera sits this far from its target (3.46 for an item, 3.64 for an
        // avatar), so a radius delta coming from an app is in these metres.
        private const float BABYLON_ORBIT_RADIUS = 3.5f;

        // Babylon feeds a zoom button's delta into an inertial offset that decays by 0.9 per frame,
        // so the radius ends up moving ten times the delta.
        private const float ZOOM_DELTA_TO_RADIUS = 10f;

        public readonly float Min;
        public readonly float Max;
        public readonly float Start;

        public ZoomRange(float min, float max, float start)
        {
            Min = min;
            Max = max;
            Start = start;
        }

        /// <summary>
        /// Babylon's <c>zoom</c> option, 0 to 100, as a factor over the fitted view.
        /// </summary>
        public static float FactorFromPercent(float percent) =>
            MIN_ZOOM_FACTOR + (MAX_ZOOM_FACTOR - MIN_ZOOM_FACTOR) * Mathf.Clamp(percent, 0f, 100f) / 100f;

        /// <summary>
        /// With neither option, the renderer's own range around the fitted view. Otherwise Babylon's
        /// rule: <paramref name="zoomPercent"/> is the closest view, the wheel pulls back from it by
        /// <paramref name="wheelZoom"/>, and <paramref name="wheelStart"/> (100 = closest) picks where
        /// to begin.
        /// </summary>
        public static ZoomRange FromOptions(float? zoomPercent, float? wheelZoom, float wheelStart,
            float defaultZoomIn, float defaultZoomOut)
        {
            if (!zoomPercent.HasValue && !wheelZoom.HasValue)
                return new ZoomRange(1f / defaultZoomOut, defaultZoomIn, 1f);

            var closest = zoomPercent.HasValue ? FactorFromPercent(zoomPercent.Value) : 1f;
            var pullBack = Mathf.Max(wheelZoom ?? 1f, 1f);
            var farthest = closest / pullBack;
            var startFraction = (100f - Mathf.Clamp(wheelStart, 0f, 100f)) / 100f;

            // Babylon starts at a radius between its limits; the factor is the inverse of the radius.
            var start = closest / (1f + (pullBack - 1f) * startFraction);

            return new ZoomRange(farthest, closest, start);
        }

        public float Clamp(float zoom) => Mathf.Clamp(zoom, Min, Max);

        /// <summary>
        /// A zoom button press as Babylon applies it: a delta that pulls the orbit closer when positive.
        /// </summary>
        public float StepByZoomDelta(float zoom, float delta) => StepByRadius(zoom, -delta * ZOOM_DELTA_TO_RADIUS);

        /// <summary>
        /// Moves the orbit by <paramref name="radiusDelta"/> metres, in Babylon's terms: closer is larger.
        /// </summary>
        public float StepByRadius(float zoom, float radiusDelta)
        {
            var closestRadius = BABYLON_ORBIT_RADIUS / Max;
            var farthestRadius = BABYLON_ORBIT_RADIUS / Min;
            var radius = Mathf.Clamp(BABYLON_ORBIT_RADIUS / zoom + radiusDelta, closestRadius, farthestRadius);

            return BABYLON_ORBIT_RADIUS / radius;
        }
    }
}
