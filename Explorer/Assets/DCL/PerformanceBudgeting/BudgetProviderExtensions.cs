using DCL.PerformanceBudgeting.AcquiredBudget;

namespace DCL.PerformanceBudgeting
{
    public static class BudgetProviderExtensions
    {
        public static bool TrySpendBudget(this IConcurrentBudgetProvider budgetProvider, out IAcquiredBudget acquiredBudget)
        {
            if (budgetProvider.TrySpendBudget())
            {
                acquiredBudget = AcquiredBudget.AcquiredBudget.Create(budgetProvider);
                return true;
            }

            acquiredBudget = null;
            return false;
        }
    }
}
