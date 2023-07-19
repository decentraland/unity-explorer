using System;

namespace ECS.BudgetProvider
{
    public class ConcurrentBudgetProvider : IConcurrentBudgetProvider
    {
        private int currentBudget;
        private readonly int maxBudget;

        public ConcurrentBudgetProvider(int initialBudget)
        {
            maxBudget = initialBudget;
            currentBudget = initialBudget;
        }

        public bool TrySpendBudget(int budgetCost = 1)
        {
            if (currentBudget - budgetCost > 0)
            {
                currentBudget--;
                return true;
            }

            return false;
        }

        public void ReleaseBudget(int budgetToRelease = 1)
        {
            if (currentBudget + budgetToRelease > maxBudget)
                throw new Exception("Tried to release more budget than the max budget allows");

            currentBudget = Math.Clamp(currentBudget + 1, 0, maxBudget);
        }
    }
}
