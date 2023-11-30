using DCL.PerformanceAndDiagnostics.Profiling;
using Utility.Multithreading;

namespace DCL.PerformanceAndDiagnostics.Optimization.PerformanceBudgeting
{
    public class FrameTimeSharedBudgetProvider : IConcurrentBudgetProvider
    {
        private readonly float totalBudgetAvailable;
        private readonly IProfilingProvider profilingProvider;
        private double currentAvailableBudget;
        private ulong startTime;
        private bool outOfBudget;

        private long currentFrameNumber;

        public FrameTimeSharedBudgetProvider(float totalBudgetAvailableInMiliseconds, IProfilingProvider profilingProvider)
        {
            //FrameTime return CurrentValue in nanoseconds, so we are converting milliseconds to nanoseconds
            totalBudgetAvailable = totalBudgetAvailableInMiliseconds * 1000000;
            currentAvailableBudget = totalBudgetAvailable;
            this.profilingProvider = profilingProvider;

            currentFrameNumber = MultithreadingUtility.FrameCount;
        }

        public bool TrySpendBudget()
        {
            TryResetBudget();

            if (outOfBudget)
                return false;

            currentAvailableBudget -= profilingProvider.CurrentFrameTimeValueInNS - startTime;
            ReleaseBudget();

            outOfBudget = currentAvailableBudget < 0;

            return true;
        }

        public void ReleaseBudget()
        {
            startTime = profilingProvider.CurrentFrameTimeValueInNS;
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
