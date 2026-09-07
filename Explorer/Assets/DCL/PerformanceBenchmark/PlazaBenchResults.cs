using System;
using System.Collections.Generic;

namespace DCL.PerformanceBenchmark
{
    /// <summary>
    ///     Serialized to results.json in the benchmark output directory. Field names are the JSON schema.
    /// </summary>
    [Serializable]
    public class PlazaBenchResults
    {
        public string Schema = "plaza-bench/1";
        public string StartedAtUtc = string.Empty;
        public string FinishedAtUtc = string.Empty;

        public string DclVersion = string.Empty;
        public string AppVersion = string.Empty;
        public string UnityVersion = string.Empty;
        public string BuildGuid = string.Empty;
        public string Platform = string.Empty;
        public string GraphicsDeviceName = string.Empty;
        public string GraphicsDeviceVersion = string.Empty;
        public string GraphicsDeviceType = string.Empty;

        public int ScreenWidth;
        public int ScreenHeight;
        public string FullScreenMode = string.Empty;
        public int QualityLevel;
        public string QualityLevelName = string.Empty;
        public int OriginalVSyncCount;
        public int OriginalTargetFrameRate;
        public bool LockstepTime;

        public float RequestedTimeOfDayNormalized;
        public float FrozenTimeOfDayNormalized;
        public bool SkyboxFrozenAtRequestedTime;

        public float[] SpawnAnchor = Array.Empty<float>();
        public float WarmupSeconds;
        public float StandSeconds;
        public float OrbitSeconds;
        public float PullBackSeconds;

        public bool FrameTimingManagerFeatureEnabled;
        public int SampleCount;
        public int InvalidFrameTimingSamples;

        /// <summary>Which sampler backs the headline CpuMs/GpuMs stats: "FrameTimingManager" or "ProfilerRecorder".</summary>
        public string PrimaryTimingSource = string.Empty;

        public FrameStats CpuMs = new ();
        public FrameStats GpuMs = new ();
        public FrameStats FrameTimingCpuMs = new ();
        public FrameStats FrameTimingGpuMs = new ();
        public FrameStats RecorderCpuMs = new ();
        public FrameStats RecorderGpuMs = new ();

        public string CsvFile = string.Empty;
        public GoldenCapture[] Goldens = Array.Empty<GoldenCapture>();

        [Serializable]
        public class FrameStats
        {
            public double Mean;
            public double Median;
            public double P95;

            /// <summary>Computes mean/median/p95 over the samples. The input list is sorted in place.</summary>
            public static FrameStats FromSamples(List<double> samples)
            {
                var stats = new FrameStats();

                if (samples.Count == 0)
                    return stats;

                double sum = 0;

                for (var i = 0; i < samples.Count; i++)
                    sum += samples[i];

                samples.Sort();

                stats.Mean = sum / samples.Count;
                stats.Median = samples.Count % 2 == 1
                    ? samples[samples.Count / 2]
                    : (samples[(samples.Count / 2) - 1] + samples[samples.Count / 2]) * 0.5;

                int p95Index = Math.Min(samples.Count - 1, (int)Math.Ceiling(samples.Count * 0.95) - 1);
                stats.P95 = samples[Math.Max(0, p95Index)];
                return stats;
            }
        }

        [Serializable]
        public class GoldenCapture
        {
            public string Name = string.Empty;
            public string File = string.Empty;
            public float PathTime;
            public float[] Position = Array.Empty<float>();
            public float[] LookAt = Array.Empty<float>();
        }
    }
}
