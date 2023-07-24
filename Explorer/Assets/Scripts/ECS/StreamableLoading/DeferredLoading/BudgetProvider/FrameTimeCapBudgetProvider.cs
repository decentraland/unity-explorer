using Cysharp.Threading.Tasks;
using ECS.Profiling;
using UnityEngine;

namespace ECS.StreamableLoading.DeferredLoading.BudgetProvider
{
    public class FrameTimeCapBudgetProvider : IConcurrentBudgetProvider
    {

        private readonly float totalBudgetAvailable;
        private readonly IProfilingProvider profilingProvider;
        private bool outOfBudget;

        private int currentFrameNumber;

        public FrameTimeCapBudgetProvider(float budgetCap, IProfilingProvider profilingProvider)
        {
            //FrameTime return CurrentValue in nanoseconds, so we are converting milliseconds to nanoseconds
            this.totalBudgetAvailable = budgetCap * 1000000;
            this.profilingProvider = profilingProvider;

            currentFrameNumber = Time.frameCount;
        }

        public bool TrySpendBudget()
        {
            TryResetBudget();
            if (outOfBudget)
                return false;

            outOfBudget = profilingProvider.GetCurrentFrameTimeValueInNS() > totalBudgetAvailable;

            return true;
        }

        public void ReleaseBudget()
        {
        }

        private void TryResetBudget()
        {
            if (currentFrameNumber != Time.frameCount)
            {
                outOfBudget = false;
                currentFrameNumber = Time.frameCount;
            }
        }

    }
}
