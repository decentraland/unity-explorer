using System;

namespace DCL.Optimization.PerformanceBudgeting
{
    public class ConcurrentLoadingPerformanceBudget : IReleasablePerformanceBudget
    {
        private readonly int maxBudget;
        private int currentBudget;

        public ConcurrentLoadingPerformanceBudget(int initialBudget)
        {
            maxBudget = initialBudget;
            currentBudget = initialBudget;
        }

        public int CurrentBudget
        {
            get
            {
                lock (this) { return currentBudget; }
            }
        }

        public bool TrySpendBudget()
        {
            lock (this)
            {
                if (currentBudget > 0)
                {
                    currentBudget--;
                    return true;
                }

                return false;
            }
        }

        public void ReleaseBudget()
        {
            lock (this)
            {
                if (currentBudget + 1 > maxBudget)
                    throw new Exception("Tried to release more budget than the max budget allows");

                currentBudget = Math.Clamp(currentBudget + 1, 0, maxBudget);
            }
        }
    }
}
