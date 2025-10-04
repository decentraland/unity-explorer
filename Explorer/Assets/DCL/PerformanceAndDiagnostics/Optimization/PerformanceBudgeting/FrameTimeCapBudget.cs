#nullable enable
using DCL.Profiling;
using System;

namespace DCL.Optimization.PerformanceBudgeting
{
    public class FrameTimeCapBudget : IPerformanceBudget
    {
        private readonly ulong totalBudgetAvailable;
        private readonly IBudgetProfiler profiler;
        //TODO: (Juani) : Maybe this could be less obscure? If it works, reuse it in all the places that check it using ILoadingStatus
        private readonly Func<bool> isLoadingScreenOn;


        public FrameTimeCapBudget(float budgetCapInMS, IBudgetProfiler profiler, Func<bool> isLoadingScreenOn) : this(
            TimeSpan.FromMilliseconds(budgetCapInMS),
            profiler,
            isLoadingScreenOn
        ) { }

        public FrameTimeCapBudget(TimeSpan totalBudgetAvailable, IBudgetProfiler profiler, Func<bool> isLoadingScreenOn) : this(
            Convert.ToUInt64(
                totalBudgetAvailable.TotalMilliseconds * 1000000 //converting milliseconds to nanoseconds
            ),
            profiler,
            isLoadingScreenOn
        ) { }

        public FrameTimeCapBudget(ulong totalBudgetAvailable, IBudgetProfiler profiler, Func<bool> isLoadingScreenOn)
        {
            this.profiler = profiler;
            this.totalBudgetAvailable = totalBudgetAvailable;
            this.isLoadingScreenOn = isLoadingScreenOn;
        }

        public bool TrySpendBudget()
        {
            //Behind loading screen we dont care about hiccups
            if (isLoadingScreenOn.Invoke())
                return true;

            return profiler.CurrentFrameTimeValueNs < totalBudgetAvailable;
        }


        public class Default : IPerformanceBudget
        {
            private readonly IPerformanceBudget performanceBudget;

            public Default() : this(new Profiler()) { }

            //33 in [ms]. Table: 33ms ~ 30fps | 16ms ~ 60fps | 11ms ~ 90 fps | 8ms ~ 120fps
            public Default(IBudgetProfiler profiler)
            {
                performanceBudget = new FrameTimeCapBudget(33f, profiler, () => false);
            }

            public bool TrySpendBudget() =>
                performanceBudget.TrySpendBudget();
        }
    }
}
