using Decentraland.Common;
using UnityEngine;

namespace DCL.SkyBox
{
    /// <summary>
    ///     Piecewise-linear color ramp built from a protocol gradient: keys sorted by time, both ends clamped,
    ///     colors kept unclamped so HDR values survive. Sampling allocates nothing.
    /// </summary>
    public sealed class ColorRamp
    {
        private readonly float[] times;
        private readonly Color[] colors;

        public int KeyCount => times.Length;

        private ColorRamp(float[] times, Color[] colors)
        {
            this.times = times;
            this.colors = colors;
        }

        /// <summary>
        ///     Null when the gradient is unset, has no keys, or none of its keys carries a color.
        /// </summary>
        public static ColorRamp? FromProto(ColorGradient? gradient)
        {
            if (gradient == null || gradient.Keys.Count == 0)
                return null;

            var validKeys = 0;

            for (var i = 0; i < gradient.Keys.Count; i++)
                if (gradient.Keys[i].Color != null)
                    validKeys++;

            if (validKeys == 0)
                return null;

            var times = new float[validKeys];
            var colors = new Color[validKeys];
            var index = 0;

            for (var i = 0; i < gradient.Keys.Count; i++)
            {
                ColorKey key = gradient.Keys[i];
                Color4? color = key.Color;

                if (color == null)
                    continue;

                times[index] = Mathf.Clamp01(key.Time);
                colors[index] = new Color(color.R, color.G, color.B, color.A);
                index++;
            }

            SortStable(times, colors);
            return new ColorRamp(times, colors);
        }

        public Color Evaluate(float t)
        {
            if (t < times[0])
                return colors[0];

            for (var i = 1; i < times.Length; i++)
            {
                if (times[i] <= t)
                    continue;

                // times[i - 1] <= t < times[i], so the span is strictly positive
                float span = times[i] - times[i - 1];
                return Color.LerpUnclamped(colors[i - 1], colors[i], (t - times[i - 1]) / span);
            }

            return colors[times.Length - 1];
        }

        /// <summary>
        ///     Insertion sort keeps keys with the same time in their original order, so the later key is the one
        ///     sampled at and after that time.
        /// </summary>
        private static void SortStable(float[] times, Color[] colors)
        {
            for (var i = 1; i < times.Length; i++)
            {
                float time = times[i];
                Color color = colors[i];
                int j = i - 1;

                while (j >= 0 && times[j] > time)
                {
                    times[j + 1] = times[j];
                    colors[j + 1] = colors[j];
                    j--;
                }

                times[j + 1] = time;
                colors[j + 1] = color;
            }
        }
    }
}
