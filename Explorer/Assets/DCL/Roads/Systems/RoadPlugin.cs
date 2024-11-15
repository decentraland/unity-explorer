using System.Collections.Generic;
using System.Threading;
using Arch.Core;
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.LOD;
using DCL.Optimization.PerformanceBudgeting;
using DCL.PluginSystem;
using DCL.PluginSystem.Global;
using DCL.ResourcesUnloading;
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

        private readonly IReadOnlyDictionary<Vector2Int, RoadDescription> roadDataDictionary;

        private readonly RoadAssetsPool roadAssetPool;

        public RoadPlugin(IPerformanceBudget frameCapBudget, IPerformanceBudget memoryBudget, IReadOnlyDictionary<Vector2Int, RoadDescription> roadDataDictionary,
            IScenesCache scenesCache, ISceneReadinessReportQueue sceneReadinessReportQueue, RoadAssetsPool roadAssetPool)
        {
            this.frameCapBudget = frameCapBudget;
            this.memoryBudget = memoryBudget;
            this.roadDataDictionary = roadDataDictionary;
            this.scenesCache = scenesCache;
            this.sceneReadinessReportQueue = sceneReadinessReportQueue;
            this.roadAssetPool = roadAssetPool;
        }

        public UniTask Initialize(IPluginSettingsContainer container, CancellationToken ct) =>
            UniTask.CompletedTask;

        public void InjectToWorld(ref ArchSystemsWorldBuilder<World> builder, in GlobalPluginArguments arguments)
        {
            RoadInstantiatorSystem.InjectToWorld(ref builder, frameCapBudget, memoryBudget, roadDataDictionary, roadAssetPool, sceneReadinessReportQueue, scenesCache);
            UnloadRoadSystem.InjectToWorld(ref builder, roadAssetPool, scenesCache);
        }

        public void Dispose()
        {
            roadAssetPool?.Dispose();
        }
    }
}
