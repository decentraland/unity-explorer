namespace DCL.Profiling
{
    public interface IProfiler : IMemoryProfiler
    {
        bool IsCollectingFrameData { get; }
        void StopFrameTimeDataCollection();
        void StartFrameTimeDataCollection();

        FrameTimeStats? CalculateMainThreadFrameTimesNs();

        HiccupStats CalculateMainThreadHiccups();

        HiccupStats CalculateGpuHiccups();

        public ulong AllScenesTotalHeapSize { get; set; }

        public ulong AllScenesTotalHeapSizeExecutable { get; set; }

        public ulong AllScenesTotalPhysicalSize { get; set; }

        public ulong AllScenesUsedHeapSize { get; set; }

        public ulong AllScenesHeapSizeLimit { get; set; }

        public ulong AllScenesTotalExternalSize { get; set; }

        public int ActiveEngines { get; set; }

        public ulong CurrentSceneTotalHeapSize { get; set; }

        public ulong CurrentSceneTotalHeapSizeExecutable { get; set; }

        public ulong CurrentSceneUsedHeapSize { get; set; }

        public bool CurrentSceneHasStats { get; set; }

        FrameTimesRecorder GpuFrameTimes { get; }
        FrameTimesRecorder MainThreadFrameTimes { get; }
        float PhysicsSimulationsAvgInTenFrames { get; }

        void UpdateFrameTimings();

        void ClearFrameTimings();
    }

    /// <summary>
    ///     Hiccup statistics over the current measurement window. All time fields are in nanoseconds.
    /// </summary>
    public readonly struct HiccupStats
    {
        /// <summary>False only when the window has no samples yet (before the first recorded frame).</summary>
        public readonly bool HasValue;

        /// <summary>Number of frames above <see cref="ThresholdNs" />.</summary>
        public readonly long Count;

        /// <summary>Sum of the full duration of every hiccup frame.</summary>
        public readonly long SumTimeNs;

        /// <summary>Sum of each hiccup frame's excess over <see cref="ThresholdNs" /> ("time lost" beyond the bar).</summary>
        public readonly long ExcessTimeNs;

        public readonly long MinNs;
        public readonly long MaxNs;
        public readonly float AvgNs;

        /// <summary>Frames the stats were measured over (can be below the buffer size during warm-up or low framerate).</summary>
        public readonly long SampleCount;

        /// <summary>The hiccup threshold actually applied, derived from the target frame rate.</summary>
        public readonly long ThresholdNs;

        public HiccupStats(bool hasValue, long count, long sumTimeNs, long excessTimeNs, long minNs, long maxNs, float avgNs, long sampleCount, long thresholdNs)
        {
            HasValue = hasValue;
            Count = count;
            SumTimeNs = sumTimeNs;
            ExcessTimeNs = excessTimeNs;
            MinNs = minNs;
            MaxNs = maxNs;
            AvgNs = avgNs;
            SampleCount = sampleCount;
            ThresholdNs = thresholdNs;
        }
    }
}
