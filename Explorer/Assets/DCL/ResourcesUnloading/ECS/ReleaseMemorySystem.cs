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
        private readonly UnloadStrategyHandler unloadStrategyHandler;

        internal ReleaseMemorySystem(Arch.Core.World world, IMemoryUsageProvider memoryBudgetProvider,
            UnloadStrategyHandler unloadStrategyHandler) : base(world)
        {
            this.memoryBudgetProvider = memoryBudgetProvider;
            this.unloadStrategyHandler = unloadStrategyHandler;
        }

        protected override void Update(float t)
        {
            if (memoryBudgetProvider.GetMemoryUsageStatus() != MemoryUsageStatus.NORMAL)
                unloadStrategyHandler.TryUnload();
            else
                unloadStrategyHandler.ResetToNormal();
        }
    }
}
