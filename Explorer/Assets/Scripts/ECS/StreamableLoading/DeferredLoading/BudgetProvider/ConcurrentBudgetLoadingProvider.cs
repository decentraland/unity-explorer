using System;

namespace ECS.StreamableLoading.DeferredLoading.BudgetProvider
{
    public class ConcurrentLoadingBudgetProvider : IConcurrentBudgetProvider
    {
        private int currentBudget;
        private readonly int maxBudget;

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
            if (currentBudget + 1 > maxBudget)
                throw new Exception("Tried to release more budget than the max budget allows");

            currentBudget = Math.Clamp(currentBudget + 1, 0, maxBudget);
        }
    }
}
