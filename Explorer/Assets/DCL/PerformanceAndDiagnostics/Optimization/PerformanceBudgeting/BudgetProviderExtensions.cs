namespace DCL.Optimization.PerformanceBudgeting
{
    public static class BudgetProviderExtensions
    {
        public static bool TrySpendBudget(this IReleasablePerformanceBudget budget, out IAcquiredBudget acquiredBudget)
        {
            if (budget.TrySpendBudget())
            {
                acquiredBudget = AcquiredBudget.Create(budget);
                return true;
            }

            acquiredBudget = null;
            return false;
        }
    }
}
