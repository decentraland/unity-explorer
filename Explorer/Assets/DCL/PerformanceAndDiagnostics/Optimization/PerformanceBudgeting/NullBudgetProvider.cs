namespace DCL.PerformanceAndDiagnostics.Optimization.PerformanceBudgeting
{
    public class NullBudgetProvider : IConcurrentBudgetProvider
    {
        public bool TrySpendBudget() =>
            true;

        public void ReleaseBudget() { }
    }
}
