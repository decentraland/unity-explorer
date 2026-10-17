namespace DCL.Optimization.PerformanceBudgeting
{
    public interface IReleasablePerformanceBudget : IPerformanceBudget
    {
        void ReleaseBudget();
    }
}
