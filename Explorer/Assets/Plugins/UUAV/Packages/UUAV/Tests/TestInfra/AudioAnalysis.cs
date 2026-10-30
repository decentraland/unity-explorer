using System;
using System.Collections.Generic;

namespace UUAV.Tests
{
    /// <summary>
    /// Reduces an interleaved capture to per-window RMS values, the unit the
    /// audio assertions reason in: a window below the silence threshold is a
    /// silent window, a run of them is a gap.
    /// </summary>
    public static class AudioAnalysis
    {
        /// <summary>Below this RMS a window counts as silent; the sine fixture plays around 0.5.</summary>
        public const float SilenceRmsThreshold = 0.05f;

        public const float WindowSeconds = 0.03f;

        public static List<float> WindowRms(float[] samples, int sampleCount, int channels, int sampleRate)
        {
            var result = new List<float>();
            if (channels <= 0 || sampleRate <= 0)
            {
                return result;
            }

            int windowSamples = Math.Max(1, (int)(sampleRate * WindowSeconds)) * channels;
            for (var start = 0; start + windowSamples <= sampleCount; start += windowSamples)
            {
                double sum = 0;
                for (int i = start; i < start + windowSamples; i++)
                {
                    sum += (double)samples[i] * samples[i];
                }

                result.Add((float)Math.Sqrt(sum / windowSamples));
            }

            return result;
        }

        /// <summary>Index of the first window at or above the silence threshold, -1 when all are silent.</summary>
        public static int FirstLoudWindow(List<float> rms)
        {
            for (var i = 0; i < rms.Count; i++)
            {
                if (rms[i] >= SilenceRmsThreshold)
                {
                    return i;
                }
            }

            return -1;
        }

        public static int CountSilentWindows(List<float> rms, int fromWindow)
        {
            var count = 0;
            for (int i = fromWindow; i < rms.Count; i++)
            {
                if (rms[i] < SilenceRmsThreshold)
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>Length in seconds of the longest silent stretch at or after <paramref name="fromWindow"/>.</summary>
        public static float LongestSilenceSeconds(List<float> rms, int fromWindow)
        {
            var longest = 0;
            var current = 0;
            for (int i = fromWindow; i < rms.Count; i++)
            {
                current = rms[i] < SilenceRmsThreshold ? current + 1 : 0;
                longest = Math.Max(longest, current);
            }

            return longest * WindowSeconds;
        }
    }
}
