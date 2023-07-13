using System;
using UnityEngine;

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

        public bool TrySpendBudget(int budgetCost)
        {
            if (currentBudget - budgetCost > 0)
            {
                currentBudget -= budgetCost;
                return true;
            }
            return false;
        }

        public void ReleaseBudget(int budgetReleased)
        {
            if (currentBudget + budgetReleased > maxBudget)
                throw new Exception("Tried to release more budget than the max budget allows");
            currentBudget = Math.Clamp(currentBudget + 1, 0, maxBudget);
        }
    }
}
