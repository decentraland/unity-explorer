using System.Collections.Generic;
using Arch.Core;
using Arch.SystemGroups;
using DCL.DebugUtilities;
using DCL.LOD;
using DCL.Optimization.PerformanceBudgeting;
using DCL.PluginSystem.Global;
using DCL.Rendering.GPUInstancing;
using DCL.Rendering.GPUInstancing.Systems;
using DCL.Roads.Settings;
using ECS.SceneLifeCycle;
using ECS.SceneLifeCycle.Reporting;
using UnityEngine;

namespace DCL.Roads.Systems
{
    public class RoadPlugin : IDCLGlobalPlugin
    {
        private readonly IPerformanceBudget frameCapBudget;
        private readonly IPerformanceBudget memoryBudget;
        private readonly IScenesCache scenesCache;
        private readonly ISceneReadinessReportQueue sceneReadinessReportQueue;
        private readonly IDebugContainerBuilder debugBuilder;

        private readonly IReadOnlyDictionary<Vector2Int, RoadDescription> roadDataDictionary;

        private readonly RoadAssetsPool roadAssetPool;
        private readonly GPUInstancingService gpuInstancingService;
        private readonly bool enableGPUInstancing;

        public RoadPlugin(IPerformanceBudget frameCapBudget, IPerformanceBudget memoryBudget, IReadOnlyDictionary<Vector2Int, RoadDescription> roadDataDictionary,
            IScenesCache scenesCache, ISceneReadinessReportQueue sceneReadinessReportQueue, RoadAssetsPool roadAssetPool, GPUInstancingService gpuInstancingService, IDebugContainerBuilder debugBuilder)
        {
            this.frameCapBudget = frameCapBudget;
            this.memoryBudget = memoryBudget;
            this.roadDataDictionary = roadDataDictionary;
            this.scenesCache = scenesCache;
            this.sceneReadinessReportQueue = sceneReadinessReportQueue;
            this.roadAssetPool = roadAssetPool;
            this.gpuInstancingService = gpuInstancingService;
            this.debugBuilder = debugBuilder;
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<World> builder, in GlobalPluginArguments arguments)
        {
            DebugGPUInstancingSystem.InjectToWorld(ref builder, debugBuilder, gpuInstancingService);
            RoadInstantiatorSystem.InjectToWorld(ref builder, frameCapBudget, memoryBudget, roadDataDictionary, roadAssetPool, sceneReadinessReportQueue, scenesCache);
        }

        public void Dispose()
        {
            roadAssetPool?.Dispose();
        }
    }
}
