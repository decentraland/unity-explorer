using DCL.Profiling;
using Utility.Multithreading;

namespace DCL.Optimization.PerformanceBudgeting
{
    public class FrameTimeSharedBudget : IReleasablePerformanceBudget
    {
        private readonly float totalBudgetAvailable;
        private readonly IBudgetProfiler profilerProvider;
        private double currentAvailableBudget;
        private ulong startTime;
        private bool outOfBudget;

        private long currentFrameNumber;

        public FrameTimeSharedBudget(float totalBudgetAvailableInMiliseconds, IBudgetProfiler profilerProvider)
        {
            //FrameTime return CurrentValue in nanoseconds, so we are converting milliseconds to nanoseconds
            totalBudgetAvailable = totalBudgetAvailableInMiliseconds * 1000000;
            currentAvailableBudget = totalBudgetAvailable;
            this.profilerProvider = profilerProvider;

            currentFrameNumber = MultithreadingUtility.FrameCount;
        }

        public bool TrySpendBudget()
        {
            TryResetBudget();

            if (outOfBudget)
                return false;

            currentAvailableBudget -= profilerProvider.CurrentFrameTimeValueNs - startTime;
            ReleaseBudget();

            outOfBudget = currentAvailableBudget < 0;

            return true;
        }

        public void ReleaseBudget()
        {
            startTime = profilerProvider.CurrentFrameTimeValueNs;
        }

        private void TryResetBudget()
        {
            if (currentFrameNumber != MultithreadingUtility.FrameCount)
            {
                currentAvailableBudget = totalBudgetAvailable;
                outOfBudget = false;
                currentFrameNumber = MultithreadingUtility.FrameCount;
            }
        }
    }
}
