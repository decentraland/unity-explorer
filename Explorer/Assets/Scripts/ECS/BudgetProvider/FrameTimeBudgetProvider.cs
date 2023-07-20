using System;
using UnityEngine;

namespace ECS.BudgetProvider
{
    public class FrameTimeBudgetProvider : IConcurrentBudgetProvider
    {
        public float currentBudget;
        private readonly float totalBudgetAvailable;
        private readonly IFrameTimeCounter frameTimeCounter;
        private float startTime;
        private bool budgetBlown;

        public FrameTimeBudgetProvider(float totalBudgetAvailableInMiliseconds, IFrameTimeCounter frameTimeCounter)
        {
            //FrameTime return CurrentValue in nanoseconds, so we are converting milliseconds to nanoseconds
            this.totalBudgetAvailable = totalBudgetAvailableInMiliseconds * 1000000;
            this.currentBudget = totalBudgetAvailable;
            this.frameTimeCounter = frameTimeCounter;
        }

        public bool TrySpendBudget(int budgetCost = 1)
        {
            if (budgetBlown)
                return false;

            currentBudget -= (frameTimeCounter.GetFrameTime() - startTime);
            budgetBlown = currentBudget < 0;
            return true;
        }

        public void ReleaseBudget(int budgetToRelease = 1)
        {
            currentBudget = totalBudgetAvailable;
            budgetBlown = false;
        }

        public void ResetBudget()
        {
            startTime = frameTimeCounter.GetFrameTime();
        }

    }
}
