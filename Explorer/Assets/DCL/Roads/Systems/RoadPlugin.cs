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
using Random = UnityEngine.Random;

namespace DCL.Roads.Systems
{
    public class RoadPlugin : IDCLGlobalPlugin<RoadSettings>
    {
        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly CacheCleaner cacheCleaner;

        private readonly IPerformanceBudget frameCapBudget;
        private readonly IPerformanceBudget memoryBudget;

        private Dictionary<Vector2Int, RoadDescription> roadDataDictionary;
        private RoadAssetsPool roadAssetPool;
        private readonly VisualSceneStateResolver visualSceneStateResolver;

        public RoadPlugin(CacheCleaner cacheCleaner, IAssetsProvisioner assetsProvisioner, IPerformanceBudget frameCapBudget, IPerformanceBudget memoryBudget, VisualSceneStateResolver visualSceneStateResolver)
        {
            this.cacheCleaner = cacheCleaner;
            this.assetsProvisioner = assetsProvisioner;
            this.frameCapBudget = frameCapBudget;
            this.memoryBudget = memoryBudget;
            this.visualSceneStateResolver = visualSceneStateResolver;
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<World> builder, in GlobalPluginArguments arguments)
        {
            RoadInstantiatorSystem.InjectToWorld(ref builder, frameCapBudget, memoryBudget, roadDataDictionary, roadAssetPool);
            UnloadRoadSystem.InjectToWorld(ref builder, roadAssetPool);
        }

        public async UniTask InitializeAsync(RoadSettings settings, CancellationToken ct)
        {
            IRoadSettingsAsset roadSettingsAsset = (await assetsProvisioner.ProvideMainAssetAsync(settings.RoadData, ct: ct)).Value;

            roadDataDictionary = new Dictionary<Vector2Int, RoadDescription>();
            foreach (var roadDescription in roadSettingsAsset.RoadDescriptions)
                roadDataDictionary.Add(roadDescription.RoadCoordinate, roadDescription);
            visualSceneStateResolver.Init(roadDataDictionary.Keys.ToHashSet());

            var roadAssetsPrefabList = new List<GameObject>();
            foreach (var assetReferenceGameObject in roadSettingsAsset.RoadAssetsReference)
                roadAssetsPrefabList.Add((await assetsProvisioner.ProvideMainAssetAsync(assetReferenceGameObject, ct: ct)).Value);
            roadAssetPool = new RoadAssetsPool(roadAssetsPrefabList);
            cacheCleaner.Register(roadAssetPool);
        }

        public void Dispose()
        {
            roadAssetPool.Dispose();
        }
    }


    [Serializable]
    public class RoadSettings : IDCLPluginSettings
    {
        [field: Header(nameof(RoadPlugin) + "." + nameof(RoadSettingsAsset))]
        [field: Space]
        [field: SerializeField]
        public StaticSettings.RoadDataRef RoadData { get; set; }
    }
}