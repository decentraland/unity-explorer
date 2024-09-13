using Arch.SystemGroups;
using DCL.Optimization.PerformanceBudgeting;
using DCL.ResourcesUnloading;
using ECS.Abstract;
using ECS.Groups;
using TMPro;
using UnityEngine;

namespace DCL.PluginSystem.Global
{
    [UpdateInGroup(typeof(CleanUpGroup))]
    public partial class ReleaseMemorySystem : BaseUnityLoopSystem
    {
        private readonly IMemoryUsageProvider memoryBudgetProvider;
        private readonly ICacheCleaner cacheCleaner;

        private float timeSinceLastUnload;
        private readonly float unloadInterval = 5f;

        internal ReleaseMemorySystem(Arch.Core.World world, ICacheCleaner cacheCleaner, IMemoryUsageProvider memoryBudgetProvider) : base(world)
        {
            this.cacheCleaner = cacheCleaner;
            this.memoryBudgetProvider = memoryBudgetProvider;
        }

        protected override void Update(float t)
        {
            timeSinceLastUnload += t;

            
            if (memoryBudgetProvider.GetMemoryUsageStatus() != MemoryUsageStatus.NORMAL)
            {
                if (timeSinceLastUnload >= unloadInterval)
                {
                    TryUnloadUnusedResources();
                    timeSinceLastUnload = 0f;
                }
                cacheCleaner.UnloadCache();
            }

            cacheCleaner.UpdateProfilingCounters();
        }

        private void TryUnloadUnusedResources()
        {
            Resources.UnloadUnusedAssets();
        }
    }
}
