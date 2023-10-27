namespace ECS.StreamableLoading.DeferredLoading.BudgetProvider
{
    internal class SystemMemoryMock : ISystemMemory
    {
        public long TotalSizeInMB { get; }

        public SystemMemoryMock(long totalSizeInMB)
        {
            TotalSizeInMB = totalSizeInMB;
        }
    }
}
