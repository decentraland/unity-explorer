using System;

namespace DCL.PerformanceAndDiagnostics.Optimization.PerformanceBudgeting
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
            if (currentBudget <= 0) return false;

            currentBudget--;
            return true;
        }

        public void ReleaseBudget()
        {
            if (currentBudget + 1 > maxBudget)
                throw new Exception("Tried to release more budget than the max budget allows");

            currentBudget = Math.Clamp(currentBudget + 1, 0, maxBudget);
        }
    }
}
