using ECS.Profiling;
using UnityEngine;

namespace ECS.StreamableLoading.DeferredLoading.BudgetProvider
{
    public class FrameTimeSharedBudgetProvider : IConcurrentBudgetProvider
    {
        private double currentAvailableBudget;
        private readonly float totalBudgetAvailable;
        private readonly IProfilingProvider profilingProvider;
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

            currentAvailableBudget -= (profilingProvider.GetCurrentFrameTimeValueInNS() - startTime);
            ReleaseBudget();

            outOfBudget = currentAvailableBudget < 0;

            return true;
        }

        public void ReleaseBudget()
        {
            startTime = profilingProvider.GetCurrentFrameTimeValueInNS();
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
