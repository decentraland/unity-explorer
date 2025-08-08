#nullable enable

using DCL.Profiling;
using System;
using UnityEngine;

namespace DCL.Optimization.PerformanceBudgeting
{
    public class FrameTimeCapBudget : IPerformanceBudget
    {
        private readonly ulong totalBudgetAvailable;
        private readonly IBudgetProfiler profiler;
        private readonly Func<bool> shouldIgnoreBudget;

        public FrameTimeCapBudget(float budgetCapInMS, IBudgetProfiler profiler, Func<bool> shouldIgnoreBudget) : this(
            TimeSpan.FromMilliseconds(budgetCapInMS),
            profiler
        )
        {
            this.shouldIgnoreBudget = shouldIgnoreBudget;
        }

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

        public bool TrySpendBudget()
        {
            if (shouldIgnoreBudget != null && shouldIgnoreBudget())
            {
                Debug.Log("JUANI FRAME TIME BUDGET IS BEING IGNORED");
                return true;
            }

            return profiler.CurrentFrameTimeValueNs < totalBudgetAvailable;
        }


        public class Default : IPerformanceBudget
        {
            private readonly IPerformanceBudget performanceBudget;

            public Default() : this(new Profiler()) { }

            //33 in [ms]. Table: 33ms ~ 30fps | 16ms ~ 60fps | 11ms ~ 90 fps | 8ms ~ 120fps
            public Default(IBudgetProfiler profiler)
            {
                performanceBudget = new FrameTimeCapBudget(33f, profiler, null);
            }

            public bool TrySpendBudget() =>
                performanceBudget.TrySpendBudget();
        }
    }
}
