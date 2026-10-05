using Decentraland.Common;
using UnityEngine;

namespace DCL.SkyBox
{
    /// <summary>
    ///     Builds a <see cref="Gradient" /> from a protocol color gradient so it can be evaluated like the ramps of a
    ///     <see cref="SkyboxLookPreset" />: keys sorted by time, both ends clamped, colors kept unclamped so HDR values
    ///     survive, alpha ignored.
    /// </summary>
    public static class ColorGradientConverter
    {
        /// <summary>
        ///     Unity gradients hold at most this many color keys; a protocol gradient with more is resampled at evenly spaced times.
        /// </summary>
        public const int MAX_KEYS = 8;

        private static readonly GradientAlphaKey[] OPAQUE = { new (1f, 0f) };

        /// <summary>
        ///     Null when the gradient is unset, has no keys, or none of its keys carries a color.
        /// </summary>
        public static Gradient? ToGradient(ColorGradient? gradient)
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
            int count = DropEarlierDuplicates(times, colors);

            GradientColorKey[] colorKeys = count > MAX_KEYS ? Resample(times, colors, count) : ToColorKeys(times, colors, count);

            var result = new Gradient { mode = GradientMode.Blend };
            result.SetKeys(colorKeys, OPAQUE);
            return result;
        }

        private static GradientColorKey[] ToColorKeys(float[] times, Color[] colors, int count)
        {
            var keys = new GradientColorKey[count];

            for (var i = 0; i < count; i++)
                keys[i] = new GradientColorKey(colors[i], times[i]);

            return keys;
        }

        /// <summary>
        ///     Evaluates the piecewise-linear ramp at <see cref="MAX_KEYS" /> evenly spaced times over [0, 1]; the end samples
        ///     equal the clamped end colors, so the ramp keeps its range.
        /// </summary>
        private static GradientColorKey[] Resample(float[] times, Color[] colors, int count)
        {
            var keys = new GradientColorKey[MAX_KEYS];

            for (var i = 0; i < MAX_KEYS; i++)
            {
                float t = i / (float)(MAX_KEYS - 1);
                keys[i] = new GradientColorKey(Evaluate(times, colors, count, t), t);
            }

            return keys;
        }

        private static Color Evaluate(float[] times, Color[] colors, int count, float t)
        {
            if (t <= times[0])
                return colors[0];

            for (var i = 1; i < count; i++)
            {
                if (times[i] < t)
                    continue;

                // times[i - 1] < t <= times[i], and duplicates were dropped, so the span is strictly positive
                float span = times[i] - times[i - 1];
                return Color.LerpUnclamped(colors[i - 1], colors[i], (t - times[i - 1]) / span);
            }

            return colors[count - 1];
        }

        /// <summary>
        ///     Insertion sort keeps keys with the same time in their original order, so the later key is the one that wins.
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

        /// <summary>
        ///     Keeps the last of the keys sharing a time; a gradient cannot hold two keys at one position.
        /// </summary>
        /// <returns>The number of keys left at the front of the arrays.</returns>
        private static int DropEarlierDuplicates(float[] times, Color[] colors)
        {
            var count = 0;

            for (var i = 0; i < times.Length; i++)
            {
                if (count > 0 && Mathf.Approximately(times[count - 1], times[i]))
                    count--;

                times[count] = times[i];
                colors[count] = colors[i];
                count++;
            }

            return count;
        }
    }
}
