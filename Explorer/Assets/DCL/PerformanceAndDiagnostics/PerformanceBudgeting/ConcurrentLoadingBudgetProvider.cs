using System;

namespace DCL.PerformanceBudgeting
{
    public class ConcurrentLoadingBudgetProvider : IConcurrentBudgetProvider
    {
        private readonly int maxBudget;
        private int currentBudget;

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

        public bool TrySpendBudget(out IAcquiredBudget acquiredBudget)
        {
            acquiredBudget = null; // Initialize the out parameter

            if (currentBudget > 0)
            {
                currentBudget--;
                acquiredBudget = AcquiredBudget.Create(this);
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
