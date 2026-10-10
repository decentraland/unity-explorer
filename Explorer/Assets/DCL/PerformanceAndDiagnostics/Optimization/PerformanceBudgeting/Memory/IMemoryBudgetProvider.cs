namespace DCL.Optimization.PerformanceBudgeting
{
    public interface IMemoryUsageProvider
    {
        bool IsInAbundance();
        bool IsMemoryNormal();
        bool IsMemoryFull();
    }
}
