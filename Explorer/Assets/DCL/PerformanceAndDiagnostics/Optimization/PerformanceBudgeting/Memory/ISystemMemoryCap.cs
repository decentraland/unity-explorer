namespace DCL.Optimization.PerformanceBudgeting
{
    public interface ISystemMemoryCap
    {
        long MemoryCapInMB { get; }
        int MemoryCap { set; }
        public MemoryCapMode Mode { set; }
    }
}