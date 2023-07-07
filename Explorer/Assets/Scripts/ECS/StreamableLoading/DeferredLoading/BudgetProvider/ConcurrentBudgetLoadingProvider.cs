using System;

namespace ECS.Prioritization.DeferredLoading
{
    public class ConcurrentLoadingBudgetProvider : IConcurrentBudgetProvider
    {
        private int currentBudget;
        private int maxBudget;

        public ConcurrentLoadingBudgetProvider(int initialBudget)
        {
            maxBudget = initialBudget;
            currentBudget = initialBudget;
        }

        public bool TrySpendBudget()
        {
            if (currentBudget > 0)
            {
                currentBudget--;
                return true;
            }

            return false;
        }

        public void ReleaseBudget()
        {
            currentBudget = Math.Clamp(currentBudget + 1, 0, maxBudget);
        }
    }
}
