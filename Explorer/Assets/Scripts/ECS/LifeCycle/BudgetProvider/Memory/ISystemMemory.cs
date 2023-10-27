namespace ECS.StreamableLoading.DeferredLoading.BudgetProvider
{
    public interface ISystemMemory
    {
        long TotalSizeInMB { get; }
    }
}
