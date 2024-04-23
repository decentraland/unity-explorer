﻿using System.Collections.Generic;
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
using UnityEngine;

namespace DCL.Roads.Systems
{
    public class RoadPlugin : IDCLGlobalPlugin
    {
        private readonly CacheCleaner cacheCleaner;

        private readonly IPerformanceBudget frameCapBudget;
        private readonly IPerformanceBudget memoryBudget;
        private readonly IReadOnlyList<GameObject> roadPrefabs;

        private readonly IReadOnlyDictionary<Vector2Int, RoadDescription> roadDataDictionary;

        public RoadAssetsPool? RoadAssetPool { get; private set; }

        public RoadPlugin(CacheCleaner cacheCleaner,
            IPerformanceBudget frameCapBudget, IPerformanceBudget memoryBudget,
            IReadOnlyList<GameObject> roadPrefabs, IReadOnlyDictionary<Vector2Int, RoadDescription> roadDataDictionary)
        {
            this.cacheCleaner = cacheCleaner;
            this.frameCapBudget = frameCapBudget;
            this.memoryBudget = memoryBudget;
            this.roadPrefabs = roadPrefabs;
            this.roadDataDictionary = roadDataDictionary;
        }

        public UniTask Initialize(IPluginSettingsContainer container, CancellationToken ct)
        {
            RoadAssetPool = new RoadAssetsPool(roadPrefabs);
            cacheCleaner.Register(RoadAssetPool);
            return UniTask.CompletedTask;
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<World> builder, in GlobalPluginArguments arguments)
        {
            RoadInstantiatorSystem.InjectToWorld(ref builder, frameCapBudget, memoryBudget, roadDataDictionary, RoadAssetPool);
            UnloadRoadSystem.InjectToWorld(ref builder, RoadAssetPool);
        }

        public void Dispose()
        {
            RoadAssetPool?.Dispose();
        }
    }
}
