using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Arch.Core;
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.LOD;
using DCL.Optimization.PerformanceBudgeting;
using DCL.PluginSystem;
using DCL.PluginSystem.Global;
using DCL.ResourcesUnloading;
using DCL.Roads.Settings;
using UnityEngine;

namespace DCL.Roads.Systems
{
    public class RoadPlugin : IDCLGlobalPluginWithoutSettings
    {
        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly CacheCleaner cacheCleaner;

        private readonly IPerformanceBudget frameCapBudget;
        private readonly IPerformanceBudget memoryBudget;
        private readonly IRoadSettingsAsset roadSettings;

        private readonly IReadOnlyDictionary<Vector2Int, RoadDescription> roadDataDictionary;

        private RoadAssetsPool? roadAssetPool;

        public RoadPlugin(CacheCleaner cacheCleaner, IAssetsProvisioner assetsProvisioner,
            IPerformanceBudget frameCapBudget, IPerformanceBudget memoryBudget,
            IRoadSettingsAsset roadSettings, IReadOnlyDictionary<Vector2Int, RoadDescription> roadDataDictionary)
        {
            this.cacheCleaner = cacheCleaner;
            this.assetsProvisioner = assetsProvisioner;
            this.frameCapBudget = frameCapBudget;
            this.memoryBudget = memoryBudget;
            this.roadSettings = roadSettings;
            this.roadDataDictionary = roadDataDictionary;
        }

        async UniTask IDCLPlugin<NoExposedPluginSettings>.InitializeAsync(NoExposedPluginSettings settings, CancellationToken ct)
        {
            var roadAssetsPrefabList = new List<GameObject>(roadSettings.RoadAssetsReference.Count);
            foreach (var assetReferenceGameObject in this.roadSettings.RoadAssetsReference)
                roadAssetsPrefabList.Add((await assetsProvisioner.ProvideMainAssetAsync(assetReferenceGameObject, ct: ct)).Value);

            roadAssetPool = new RoadAssetsPool(roadAssetsPrefabList);
            cacheCleaner.Register(roadAssetPool);
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<World> builder, in GlobalPluginArguments arguments)
        {
            RoadInstantiatorSystem.InjectToWorld(ref builder, frameCapBudget, memoryBudget, roadDataDictionary, roadAssetPool);
            UnloadRoadSystem.InjectToWorld(ref builder, roadAssetPool);
        }

        public void Dispose()
        {
            roadAssetPool?.Dispose();
        }
    }
}
