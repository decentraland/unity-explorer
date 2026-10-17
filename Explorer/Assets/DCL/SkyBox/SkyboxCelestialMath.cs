using UnityEngine;

namespace DCL.SkyBox
{
    /// <summary>
    ///     Pure maths of the computed celestial path: where a body sits on its arc and how the light hands over between
    ///     sun and moon. Times are normalized time of day (0 = midnight, 0.5 = noon) unless stated.
    /// </summary>
    internal static class SkyboxCelestialMath
    {
        /// <summary>
        ///     Fraction of the swap window at which the light direction starts swinging from one body to the other,
        ///     so the swing happens while the dip is deepest.
        /// </summary>
        public const float SWAP_SWING_START = 0.3f;

        /// <summary>Fraction of the swap window at which the swing is complete.</summary>
        public const float SWAP_SWING_END = 0.7f;

        /// <summary>Fraction of the swap window the crossover dip takes to ramp in at the start and out at the end.</summary>
        public const float SWAP_DIP_RAMP = 0.25f;

        /// <summary>Moon weight above which the disc shows the moon instead of the sun.</summary>
        public const float MOON_ACTIVE_WEIGHT = 0.5f;

        /// <summary>Smallest fraction of the day a body spends above or below the horizon, so progress never divides by zero.</summary>
        public const float MIN_ARC_FRACTION = 1e-4f;

        /// <summary>
        ///     Weight of the moon in the light direction and the crossover dip, from the two swap windows: the one
        ///     ending at moonrise and the one starting at moonset. Outside both, the weight is 1 while the moon is up.
        /// </summary>
        public static void EvaluateSwap(float timeOfDay, float moonriseTime, float moonsetTime, float swapDuration, out float moonWeight, out float dip)
        {
            float evening = Wrap01(timeOfDay - Wrap01(moonriseTime - swapDuration)) / swapDuration;
            float morning = Wrap01(timeOfDay - moonsetTime) / swapDuration;

            if (evening < 1f)
            {
                moonWeight = Smooth01(SWAP_SWING_START, SWAP_SWING_END, evening);
                dip = SwapDip(evening);
                return;
            }

            if (morning < 1f)
            {
                moonWeight = 1f - Smooth01(SWAP_SWING_START, SWAP_SWING_END, morning);
                dip = SwapDip(morning);
                return;
            }

            bool moonUp = Wrap01(timeOfDay - moonriseTime) < Wrap01(moonsetTime - moonriseTime);
            moonWeight = moonUp ? 1f : 0f;
            dip = 0f;
        }

        /// <summary>0 at the edges of a swap window, 1 across its middle.</summary>
        public static float SwapDip(float progress) =>
            Smooth01(0f, SWAP_DIP_RAMP, progress) * (1f - Smooth01(1f - SWAP_DIP_RAMP, 1f, progress));

        /// <summary>
        ///     Body progress on its circle: 0..1 from rise to set above the horizon, 1..2 below it until the next rise.
        /// </summary>
        public static float CelestialProgress(float timeOfDay, float rise, float set)
        {
            float above = Mathf.Max(Wrap01(set - rise), MIN_ARC_FRACTION);
            float sinceRise = Wrap01(timeOfDay - rise);

            if (sinceRise < above)
                return sinceRise / above;

            return 1f + ((sinceRise - above) / Mathf.Max(1f - above, MIN_ARC_FRACTION));
        }

        /// <summary>
        ///     Direction to a body on a great-circle arc from the rise point on the horizon at the azimuth (degrees,
        ///     0 = +Z, 90 = +X) to the opposite point, leaning sideways by the tilt. Progress 0..2 walks the full circle.
        /// </summary>
        public static Vector3 ArcDirection(float progress, float azimuthDeg, float tiltDeg)
        {
            float azimuth = azimuthDeg * Mathf.Deg2Rad;
            float tilt = tiltDeg * Mathf.Deg2Rad;
            var rise = new Vector3(Mathf.Sin(azimuth), 0f, Mathf.Cos(azimuth));
            var side = new Vector3(Mathf.Cos(azimuth), 0f, -Mathf.Sin(azimuth));
            Vector3 peak = (Mathf.Cos(tilt) * Vector3.up) + (Mathf.Sin(tilt) * side);
            float angle = progress * Mathf.PI;
            return (Mathf.Cos(angle) * rise) + (Mathf.Sin(angle) * peak);
        }

        public static float Wrap01(float value) =>
            value - Mathf.Floor(value);

        /// <summary>HLSL-style smoothstep: 0 below edge0, 1 above edge1, eased in between.</summary>
        public static float Smooth01(float edge0, float edge1, float value)
        {
            float t = Mathf.Clamp01((value - edge0) / (edge1 - edge0));
            return t * t * (3f - (2f * t));
        }
    }
}
