namespace DCL.Optimization.PerformanceBudgeting
{
    /// <summary>
    ///     Stub that always allows spending. Use for WebGL or when budget should not block.
    /// </summary>
    public class StubPerformanceBudget : IPerformanceBudget
    {
        public bool TrySpendBudget() =>
            true;
    }
}
