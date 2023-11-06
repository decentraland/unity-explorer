namespace DCL.PerformanceBudgeting.BudgetProvider
{
    public class NullBudgetProvider : IConcurrentBudgetProvider
    {
        public bool TrySpendBudget() =>
            true;

        public void ReleaseBudget() { }
    }
}
