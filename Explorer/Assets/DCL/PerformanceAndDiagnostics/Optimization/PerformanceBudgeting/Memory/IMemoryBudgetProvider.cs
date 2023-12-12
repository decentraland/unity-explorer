namespace DCL.Optimization.PerformanceBudgeting
{
    public interface IMemoryUsageProvider
    {
        MemoryUsageStatus GetMemoryUsageStatus();
    }
}
