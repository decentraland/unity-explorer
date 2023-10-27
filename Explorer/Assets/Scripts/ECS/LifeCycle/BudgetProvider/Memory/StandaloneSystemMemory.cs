using UnityEngine.Device;

namespace ECS.StreamableLoading.DeferredLoading.BudgetProvider
{
    public class StandaloneSystemMemory : ISystemMemory
    {
        public long TotalSizeInMB => SystemInfo.systemMemorySize;
    }
}
