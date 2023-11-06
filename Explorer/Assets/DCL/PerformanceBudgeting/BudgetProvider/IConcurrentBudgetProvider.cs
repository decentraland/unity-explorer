namespace DCL.PerformanceBudgeting.BudgetProvider
{
    public interface IConcurrentBudgetProvider
    {
        bool TrySpendBudget();

        void ReleaseBudget();
    }
}
