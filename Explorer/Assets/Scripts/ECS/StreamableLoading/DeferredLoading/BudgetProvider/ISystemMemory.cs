namespace ECS.StreamableLoading.DeferredLoading.BudgetProvider
{
    public interface ISystemMemory
    {
        long TotalSizeInMB { get; }
    }

    internal class SystemMemoryMock : ISystemMemory
    {
        public long TotalSizeInMB { get; }

        public SystemMemoryMock(long totalSizeInMB)
        {
            TotalSizeInMB = totalSizeInMB;
        }
    }
}
