using UnityEngine;

namespace DCL.Chat.ChatReactions.Simulation
{
    internal static class MathUtils
    {
        public const float TWO_PI = Mathf.PI * 2f;

        /// <summary>
        /// Bakes an AnimationCurve (t=0..1) into a flat float[] lookup table.
        /// Eliminates managed→native interop per evaluation; the LUT stays in CPU cache.
        /// Returns null if the curve is empty or null.
        /// </summary>
        public static float[]? BakeCurve(AnimationCurve? curve, int resolution)
        {
            if (curve == null || curve.length == 0)
                return null;

            var lut = new float[resolution];

            for (int i = 0; i < resolution; i++)
            {
                float t = (float)i / (resolution - 1);
                lut[i] = curve.Evaluate(t);
            }

            return lut;
        }
    }
}
