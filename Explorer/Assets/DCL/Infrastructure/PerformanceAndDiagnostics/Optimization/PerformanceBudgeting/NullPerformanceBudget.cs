namespace DCL.Optimization.PerformanceBudgeting
{
    public class NullPerformanceBudget : IReleasablePerformanceBudget
    {
        public bool TrySpendBudget() =>
            true;

        public void ReleaseBudget() { }
    }
}
