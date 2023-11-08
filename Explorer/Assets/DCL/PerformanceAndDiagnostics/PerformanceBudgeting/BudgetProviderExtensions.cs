namespace DCL.PerformanceBudgeting
{
    public static class BudgetProviderExtensions
    {
        public static bool TrySpendBudget(this IConcurrentBudgetProvider budgetProvider, out IAcquiredBudget acquiredBudget)
        {
            if (budgetProvider.TrySpendBudget())
            {
                acquiredBudget = AcquiredBudget.Create(budgetProvider);
                return true;
            }

            acquiredBudget = null;
            return false;
        }
    }
}
