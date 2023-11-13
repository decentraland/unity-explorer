namespace DCL.PerformanceBudgeting
{
    public interface IConcurrentBudgetProvider
    {
        bool TrySpendBudget();

        void ReleaseBudget();
    }
}
