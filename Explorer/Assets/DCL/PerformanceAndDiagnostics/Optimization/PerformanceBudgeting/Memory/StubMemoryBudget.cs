using DCL.Profiling;
using System;
using System.Collections.Generic;
using static DCL.Optimization.PerformanceBudgeting.MemoryUsageStatus;

namespace DCL.Optimization.PerformanceBudgeting
{
    /// <summary>
    ///     Stub MemoryBudget that always allows spending. Use for WebGL or when budget should not block.
    /// </summary>
    public class StubMemoryBudget : MemoryBudget
    {
        private static readonly IReadOnlyDictionary<MemoryUsageStatus, float> DEFAULT_THRESHOLDS = new Dictionary<MemoryUsageStatus, float>
        {
            { ABUNDANCE, 0.65f },
            { WARNING, 0.7f },
            { FULL, 0.75f }
        };

        public StubMemoryBudget()
            : base(new StubSystemMemoryCap(), new StubBudgetProfiler(), DEFAULT_THRESHOLDS)
        { }

        public override bool TrySpendBudget() =>
            true;

        private class StubSystemMemoryCap : ISystemMemoryCap
        {
            public long MemoryCapInMB => 4 * 1024L;
            public int MemoryCap { set => throw new NotImplementedException(); }
        }

        private class StubBudgetProfiler : IBudgetProfiler
        {
            public long TotalUsedMemoryInBytes => 0;
            public long SystemUsedMemoryInBytes => 0;
            public ulong CurrentFrameTimeValueNs => 0;
            public ulong LastFrameTimeValueNs => 0;
            public ulong LastGpuFrameTimeValueNs => 0;
            public void Dispose() { }
        }
    }
}
