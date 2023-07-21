using Cysharp.Threading.Tasks;
using ECS.Profiling;
using System;
using UnityEngine;

namespace ECS.StreamableLoading.DeferredLoading.BudgetProvider
{
    public class FrameTimeBudgetProvider : IConcurrentBudgetProvider
    {
        private double currentAvailableBudget;
        private readonly float totalBudgetAvailable;
        private readonly IProfilingProvider profilingProvider;
        private long startTime;
        private bool outOfBudget;

        public FrameTimeBudgetProvider(float totalBudgetAvailableInMiliseconds, IProfilingProvider profilingProvider)
        {
            //FrameTime return CurrentValue in nanoseconds, so we are converting milliseconds to nanoseconds
            this.totalBudgetAvailable = totalBudgetAvailableInMiliseconds * 1000000;
            this.currentAvailableBudget = totalBudgetAvailable;
            this.profilingProvider = profilingProvider;
            ResetBudgetAtTheEndOfFrame().Forget();
        }

        public bool TrySpendBudget()
        {
            if (outOfBudget)
                return false;

            currentAvailableBudget -= (profilingProvider.GetCurrentFrameTimeValue() - startTime);
            ResetBudget();
            outOfBudget = currentAvailableBudget < 0;

            return true;
        }

        public void ReleaseBudget()
        {
            startTime = profilingProvider.GetCurrentFrameTimeValue();
        }

        private void ResetBudget()
        {
            currentAvailableBudget = totalBudgetAvailable;
            outOfBudget = false;
        }

        private async UniTaskVoid ResetBudgetAtTheEndOfFrame()
        {
            while (true)
            {
                await UniTask.Yield();
                ResetBudget();
            }
        }

    }
}
