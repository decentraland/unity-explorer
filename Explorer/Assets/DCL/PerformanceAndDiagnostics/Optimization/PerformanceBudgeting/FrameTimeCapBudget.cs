#nullable enable

using DCL.Profiling;
using System;

namespace DCL.Optimization.PerformanceBudgeting
{
    public class FrameTimeCapBudget : IPerformanceBudget
    {
        private readonly ulong totalBudgetAvailable;
        private readonly IProfilingProvider profilingProvider;

        public FrameTimeCapBudget(float budgetCapInMS, IProfilingProvider profilingProvider) : this(
            TimeSpan.FromMilliseconds(budgetCapInMS),
            profilingProvider
        ) { }

        public FrameTimeCapBudget(TimeSpan totalBudgetAvailable, IProfilingProvider profilingProvider) : this(
            Convert.ToUInt64(
                totalBudgetAvailable.TotalMilliseconds * 1000000 //converting milliseconds to nanoseconds
            ),
            profilingProvider
        ) { }

        public FrameTimeCapBudget(ulong totalBudgetAvailable, IProfilingProvider profilingProvider)
        {
            this.profilingProvider = profilingProvider;
            this.totalBudgetAvailable = totalBudgetAvailable;
        }

        public bool TrySpendBudget() =>
            profilingProvider.CurrentFrameTimeValueInNS < totalBudgetAvailable;

        public class Default : IPerformanceBudget
        {
            private readonly IPerformanceBudget performanceBudget;

            public Default() : this(new ProfilingProvider()) { }

            //33 in [ms]. Table: 33ms ~ 30fps | 16ms ~ 60fps | 11ms ~ 90 fps | 8ms ~ 120fps
            public Default(IProfilingProvider profilingProvider)
            {
                performanceBudget = new FrameTimeCapBudget(33f, profilingProvider);
            }

            public bool TrySpendBudget() =>
                performanceBudget.TrySpendBudget();
        }
    }
}
