using DCL.Profiling;
using System;
using UnityEngine;

namespace DCL.Optimization.PerformanceBudgeting
{
    public class FrameTimeCapBudget : IPerformanceBudget
    {
        private static bool isQuitting;

        static FrameTimeCapBudget()
        {
            Application.quitting += () => isQuitting = true;
        }

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

        //33 in [ms]. Table: 33ms ~ 30fps | 16ms ~ 60fps | 11ms ~ 90 fps | 8ms ~ 120fps
        public static FrameTimeCapBudget NewDefault() =>
            new (33f, new Profiler());

        public bool TrySpendBudget()
        {
            // It doesn't matter to keep the budget if application is quitting
            if (isQuitting)
                return true;

            return profiler.CurrentFrameTimeValueNs < totalBudgetAvailable;
        }
    }
}
