using Cysharp.Threading.Tasks;
using ECS.Profiling;
using UnityEngine;

namespace ECS.StreamableLoading.DeferredLoading.BudgetProvider
{
    public class FrameTimeCapBudgetProvider : IConcurrentBudgetProvider
    {

        private readonly float totalBudgetAvailable;
        private readonly IProfilingProvider profilingProvider;

        public FrameTimeCapBudgetProvider(float budgetCap, IProfilingProvider profilingProvider)
        {
            //FrameTime return CurrentValue in nanoseconds, so we are converting milliseconds to nanoseconds
            this.totalBudgetAvailable = budgetCap * 1000000;
            this.profilingProvider = profilingProvider;
        }

        public bool TrySpendBudget() =>
            profilingProvider.GetCurrentFrameTimeValueInNS() < totalBudgetAvailable;

        public void ReleaseBudget()
        {
        }


    }
}
