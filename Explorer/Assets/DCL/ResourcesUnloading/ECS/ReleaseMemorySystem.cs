using Arch.SystemGroups;
using DCL.Optimization.PerformanceBudgeting;
using DCL.ResourcesUnloading;
using DCL.ResourcesUnloading.UnloadStrategies;
using ECS.Abstract;
using ECS.Groups;

namespace DCL.PluginSystem.Global
{
    [UpdateInGroup(typeof(CleanUpGroup))]
    public partial class ReleaseMemorySystem : BaseUnityLoopSystem
    {
        private readonly IMemoryUsageProvider memoryBudgetProvider;
        private readonly ICacheCleaner cacheCleaner;

        private readonly IUnloadStrategy[] unloadStrategyPriority;
        private int currentUnloadStrategy;

        private int consecutiveFailedFrames;
        private readonly int failureThreshold = 60;   

        internal ReleaseMemorySystem(Arch.Core.World world, ICacheCleaner cacheCleaner, IMemoryUsageProvider memoryBudgetProvider) : base(world)
        {
            this.cacheCleaner = cacheCleaner;
            this.memoryBudgetProvider = memoryBudgetProvider;
            unloadStrategyPriority = new IUnloadStrategy[]
            {
                new StandardUnloadStrategy(),
                new AggressiveUnloadStrategy()
            };
            currentUnloadStrategy = 0;
        }

        protected override void Update(float t)
        {
            if (unloadStrategyPriority[currentUnloadStrategy].isRunning)
                return;

            if (memoryBudgetProvider.GetMemoryUsageStatus() != MemoryUsageStatus.NORMAL)
            {
                unloadStrategyPriority[currentUnloadStrategy].TryUnload(cacheCleaner);
                consecutiveFailedFrames++;

                if (consecutiveFailedFrames >= failureThreshold)
                {
                    if (currentUnloadStrategy < unloadStrategyPriority.Length - 1)
                        currentUnloadStrategy++;

                    consecutiveFailedFrames = 0;
                }
            }
            else
            {
                currentUnloadStrategy = 0;
                consecutiveFailedFrames = 0;
            }
        }
    }
}
