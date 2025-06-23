namespace DCL.Optimization.PerformanceBudgeting
{
    public class NullPerformanceBudget : IReleasablePerformanceBudget
    {
        public static readonly NullPerformanceBudget INSTANCE = new ();

        private NullPerformanceBudget() { }

        public bool TrySpendBudget() =>
            true;

        public void ReleaseBudget() { }
    }
}
