namespace DCL.PerformanceBudgeting
{
    public class NullBudgetProvider : IConcurrentBudgetProvider
    {
        public bool TrySpendBudget() =>
            true;

        public void ReleaseBudget() { }
    }
}
