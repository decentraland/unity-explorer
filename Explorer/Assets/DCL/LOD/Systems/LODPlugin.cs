using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Arch.Core;
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.DebugUtilities;
using DCL.Ipfs;
using DCL.LOD.Systems;
using DCL.Optimization.PerformanceBudgeting;
using DCL.PluginSystem;
using DCL.PluginSystem.Global;
using DCL.ResourcesUnloading;
using ECS;
using ECS.Prioritization;
using ECS.SceneLifeCycle;
using ECS.SceneLifeCycle.Reporting;
using ECS.SceneLifeCycle.Systems;
using Newtonsoft.Json;
using UnityEngine;

namespace DCL.LOD
{
    public class LODPlugin : IDCLGlobalPlugin<LODSettings>
    {
        private ILODSettingsAsset lodSettingsAsset;
        private readonly IAssetsProvisioner assetsProvisioner;

        private readonly LODAssetsPool lodAssetsPool;
        private readonly IScenesCache scenesCache;
        private readonly IRealmData realmData;
        private readonly IPerformanceBudget frameCapBudget;
        private readonly IPerformanceBudget memoryBudget;
        private readonly IDebugContainerBuilder debugBuilder;
        private readonly ISceneReadinessReportQueue sceneReadinessReportQueue;

        private VisualSceneStateResolver visualSceneStateResolver;

        public LODPlugin(CacheCleaner cacheCleaner, RealmData realmData, IPerformanceBudget memoryBudget,
            IPerformanceBudget frameCapBudget, IScenesCache scenesCache, IDebugContainerBuilder debugBuilder, IAssetsProvisioner assetsProvisioner, ISceneReadinessReportQueue sceneReadinessReportQueue)
        {
            lodAssetsPool = new LODAssetsPool();
            cacheCleaner.Register(lodAssetsPool);

            this.realmData = realmData;
            this.memoryBudget = memoryBudget;
            this.frameCapBudget = frameCapBudget;
            this.scenesCache = scenesCache;
            this.debugBuilder = debugBuilder;
            this.assetsProvisioner = assetsProvisioner;
            this.sceneReadinessReportQueue = sceneReadinessReportQueue;
        }

        public async UniTask InitializeAsync(LODSettings settings, CancellationToken ct)
        {
            lodSettingsAsset = (await assetsProvisioner.ProvideMainAssetAsync(settings.LodSettingAsset, ct: ct)).Value;
            string roadCoordinatesText = (await assetsProvisioner.ProvideMainAssetAsync(settings.RoadCoordinatesAsset, ct: ct)).Value.text;

            List<string> roadCoordinatesList = JsonConvert.DeserializeObject<List<string>>(roadCoordinatesText);
            HashSet<Vector2Int> roadCoordinatesHashSet = new HashSet<Vector2Int>();
            foreach (string roadCoordinate in roadCoordinatesList)
            {
                roadCoordinatesHashSet.Add(IpfsHelper.DecodePointer(roadCoordinate));
            }
            
            visualSceneStateResolver = new VisualSceneStateResolver(roadCoordinatesHashSet);
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<World> builder, in GlobalPluginArguments arguments)
        {
            var lodContainer = new GameObject("POOL_CONTAINER_LODS");
            var lodDebugContainer = new GameObject("POOL_CONTAINER_LODS");
            var roadContainer = new GameObject("POOL_CONTAINER_ROADS");
            lodDebugContainer.transform.SetParent(lodContainer.transform);
            
            ResolveVisualSceneStateSystem.InjectToWorld(ref builder, lodSettingsAsset, visualSceneStateResolver);
            UpdateVisualSceneStateSystem.InjectToWorld(ref builder, realmData, scenesCache, lodAssetsPool, lodSettingsAsset, visualSceneStateResolver);
            UpdateSceneLODInfoSystem.InjectToWorld(ref builder, lodAssetsPool, lodSettingsAsset, memoryBudget, frameCapBudget, scenesCache, sceneReadinessReportQueue, lodContainer.transform);
            UnloadSceneLODSystem.InjectToWorld(ref builder, lodAssetsPool, scenesCache);
            LODDebugToolsSystem.InjectToWorld(ref builder, debugBuilder, lodSettingsAsset, lodDebugContainer.transform);

            RoadInstantiatorSystem.InjectToWorld(ref builder, frameCapBudget, memoryBudget, roadContainer.transform);
            UnloadRoadSystem.InjectToWorld(ref builder);
        }

        public void Dispose()
        {
        }
    }

    [Serializable]
    public class LODSettings : IDCLPluginSettings
    {
        [field: Header(nameof(LODPlugin) + "." + nameof(LODSettings))]
        [field: Space]
        [field: SerializeField]
        public StaticSettings.LODSettingsRef LodSettingAsset { get; set; }
        [field: Space]
        [field: SerializeField]
        public AssetReferenceTextAsset RoadCoordinatesAsset { get; set; }
    }
}
