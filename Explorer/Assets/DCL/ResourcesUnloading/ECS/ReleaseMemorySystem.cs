using System;
using Arch.SystemGroups;
using DCL.Optimization.PerformanceBudgeting;
using DCL.ResourcesUnloading;
using DCL.ResourcesUnloading.UnloadStrategies;
using ECS.Abstract;
using ECS.Groups;
using UnityEngine;

namespace DCL.PluginSystem.Global
{
    [UpdateInGroup(typeof(CleanUpGroup))]
    public partial class ReleaseMemorySystem : BaseUnityLoopSystem
    {
        private readonly IMemoryUsageProvider memoryBudgetProvider;
        private readonly ICacheCleaner cacheCleaner;

        private readonly IUnloadStrategy[] unloadStrategies;
        internal int currentUnloadStrategy;

        private int consecutiveFailedFrames;
        private readonly int failureThreshold;

        internal ReleaseMemorySystem(Arch.Core.World world, ICacheCleaner cacheCleaner,
            IMemoryUsageProvider memoryBudgetProvider, IUnloadStrategy[] unloadStrategies,
            int failuresFrameThreshold) : base(world)
        {
            this.cacheCleaner = cacheCleaner;
            this.memoryBudgetProvider = memoryBudgetProvider;
            this.unloadStrategies = unloadStrategies;
            failureThreshold = failuresFrameThreshold;
            currentUnloadStrategy = 0;
        }

        protected override void Update(float t)
        {
            if (unloadStrategies[currentUnloadStrategy].isRunning)
                return;

            if (memoryBudgetProvider.GetMemoryUsageStatus() != MemoryUsageStatus.NORMAL)
            {
                unloadStrategies[currentUnloadStrategy].TryUnload(cacheCleaner);
                consecutiveFailedFrames++;

                if (consecutiveFailedFrames >= failureThreshold)
                {
                    currentUnloadStrategy = Math.Clamp(currentUnloadStrategy + 1, 0, unloadStrategies.Length - 1);
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
