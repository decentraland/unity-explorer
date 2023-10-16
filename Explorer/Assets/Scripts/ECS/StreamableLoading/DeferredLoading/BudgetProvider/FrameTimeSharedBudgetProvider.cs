using ECS.Profiling;
using UnityEngine;

namespace ECS.StreamableLoading.DeferredLoading.BudgetProvider
{
    public class FrameTimeSharedBudgetProvider : IConcurrentBudgetProvider
    {
        private readonly float totalBudgetAvailable;
        private readonly IProfilingProvider profilingProvider;
        private double currentAvailableBudget;
        private long startTime;
        private bool outOfBudget;

        private int currentFrameNumber;

        public FrameTimeSharedBudgetProvider(float totalBudgetAvailableInMiliseconds, IProfilingProvider profilingProvider)
        {
            //FrameTime return CurrentValue in nanoseconds, so we are converting milliseconds to nanoseconds
            this.totalBudgetAvailable = totalBudgetAvailableInMiliseconds * 1000000;
            this.currentAvailableBudget = totalBudgetAvailable;
            this.profilingProvider = profilingProvider;

            currentFrameNumber = Time.frameCount;
        }

        public bool TrySpendBudget()
        {
            TryResetBudget();

            if (outOfBudget)
                return false;

            currentAvailableBudget -= (profilingProvider.CurrentFrameTimeValueInNS - startTime);
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
            if (currentFrameNumber != Time.frameCount)
            {
                currentAvailableBudget = totalBudgetAvailable;
                outOfBudget = false;
                currentFrameNumber = Time.frameCount;
            }
        }
    }
}
