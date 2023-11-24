namespace DCL.PerformanceAndDiagnostics.Optimization.PerformanceBudgeting
{
    public interface IConcurrentBudgetProvider
    {
        bool TrySpendBudget();

        void ReleaseBudget();
    }
}
