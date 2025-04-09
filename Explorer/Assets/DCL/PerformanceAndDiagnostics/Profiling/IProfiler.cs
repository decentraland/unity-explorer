﻿namespace DCL.Profiling
{
    public interface IProfiler : IMemoryProfiler
    {
        FrameTimeStats? CalculateMainThreadFrameTimesNs();

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
    }
}
