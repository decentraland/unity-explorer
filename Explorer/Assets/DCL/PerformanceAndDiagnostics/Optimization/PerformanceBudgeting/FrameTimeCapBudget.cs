using DCL.Profiling;
using System;
using UnityEngine;
using Utility.Multithreading;

namespace DCL.Optimization.PerformanceBudgeting
{
    public class FrameTimeCapBudget : IPerformanceBudget
    {
        private readonly ulong totalBudgetAvailable;
        private readonly IBudgetProfiler profiler;
        private readonly Func<bool> isLoadingScreenOn;

        private int cachedFrameNumber = -1;
        private bool cachedIsLoadingScreenOn;
        private bool cachedIsWithinBudget;

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
            //Check only on frame change to avoid multiple calls per frame (checking the loading screen status can be expensive)
            int currentFrame = Time.frameCount;
            if (cachedFrameNumber != currentFrame)
            {
                cachedIsLoadingScreenOn = isLoadingScreenOn.Invoke();
                cachedIsWithinBudget = profiler.CurrentFrameTimeValueNs < totalBudgetAvailable;
                cachedFrameNumber = currentFrame;
            }

            if (cachedIsLoadingScreenOn)
                return true;

            return cachedIsWithinBudget;
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
