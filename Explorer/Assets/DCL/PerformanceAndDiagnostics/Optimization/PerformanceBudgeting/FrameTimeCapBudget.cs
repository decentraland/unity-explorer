#nullable enable

using DCL.Profiling;
using System;

namespace DCL.Optimization.PerformanceBudgeting
{
    public class FrameTimeCapBudget : IPerformanceBudget
    {
        private readonly ulong totalBudgetAvailable;
        private readonly IBudgetProfiler profiler;

        public FrameTimeCapBudget(float budgetCapInMS, IBudgetProfiler profiler) : this(
            TimeSpan.FromMilliseconds(budgetCapInMS),
            profiler
        ) { }

        public FrameTimeCapBudget(TimeSpan totalBudgetAvailable, IBudgetProfiler profiler) : this(
            Convert.ToUInt64(
                totalBudgetAvailable.TotalMilliseconds * 1000000 //converting milliseconds to nanoseconds
            ),
            profiler
        ) { }

        public FrameTimeCapBudget(ulong totalBudgetAvailable, IBudgetProfiler profiler)
        {
            this.profiler = profiler;
            this.totalBudgetAvailable = totalBudgetAvailable;
        }

        public bool TrySpendBudget() =>
             profiler.CurrentFrameTimeValueNs < totalBudgetAvailable;

        public class Default : IPerformanceBudget
        {
            private readonly IPerformanceBudget performanceBudget;

            public Default() : this(new Profiler()) { }

            //33 in [ms]. Table: 33ms ~ 30fps | 16ms ~ 60fps | 11ms ~ 90 fps | 8ms ~ 120fps
            public Default(IBudgetProfiler profiler)
            {
                performanceBudget = new FrameTimeCapBudget(33f, profiler);
            }

            public bool TrySpendBudget() =>
                performanceBudget.TrySpendBudget();
        }
    }
}
