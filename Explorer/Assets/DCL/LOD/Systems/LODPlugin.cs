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
using DCL.Optimization.Pools;
using DCL.PluginSystem;
using DCL.PluginSystem.Global;
using DCL.ResourcesUnloading;
using DCL.Roads.Settings;
using ECS;
using ECS.Prioritization;
using ECS.SceneLifeCycle;
using ECS.SceneLifeCycle.Reporting;
using ECS.SceneLifeCycle.Systems;
using Newtonsoft.Json;
using UnityEngine;
using Utility;

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

        private IExtendedObjectPool<Material> lodMaterialPool;
        
        private VisualSceneStateResolver visualSceneStateResolver;


        public LODPlugin(CacheCleaner cacheCleaner, RealmData realmData, IPerformanceBudget memoryBudget,
            IPerformanceBudget frameCapBudget, IScenesCache scenesCache, IDebugContainerBuilder debugBuilder, IAssetsProvisioner assetsProvisioner, ISceneReadinessReportQueue sceneReadinessReportQueue, VisualSceneStateResolver visualSceneStateResolver)
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
            this.visualSceneStateResolver = visualSceneStateResolver;
        }

        public async UniTask InitializeAsync(LODSettings settings, CancellationToken ct)
        {
            lodSettingsAsset = (await assetsProvisioner.ProvideMainAssetAsync(settings.LODSettingAsset, ct: ct)).Value;

            await CreateMaterialPoolPrewarmedAsync(settings, ct);
        }

        private async UniTask CreateMaterialPoolPrewarmedAsync(LODSettings settings, CancellationToken ct)
        {
            ProvidedAsset<Material> providedMaterial = await assetsProvisioner.ProvideMainAssetAsync(settings.lodMaterial, ct: ct);

            lodMaterialPool = new ExtendedObjectPool<Material>(
                () => new Material(providedMaterial.Value),
                actionOnRelease: mat =>
                {
                    // reset material so it does not contain any old properties
                    mat.CopyPropertiesFromMaterial(providedMaterial.Value);
                },
                actionOnDestroy: UnityObjectUtils.SafeDestroy,
                defaultCapacity: 250);

            var prewarmedMaterials = new Material[250];
            for (int i = 0; i < 250; i++)
                prewarmedMaterials[i] = lodMaterialPool.Get();

            for (int i = 0; i < 250; i++)
                lodMaterialPool.Release(prewarmedMaterials[i]);
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<World> builder, in GlobalPluginArguments arguments)
        {
            var lodContainer = new GameObject("POOL_CONTAINER_LODS");
            var lodDebugContainer = new GameObject("POOL_CONTAINER_DEBUG_LODS");
            lodDebugContainer.transform.SetParent(lodContainer.transform);

            ResolveVisualSceneStateSystem.InjectToWorld(ref builder, lodSettingsAsset, visualSceneStateResolver, realmData);
            UpdateVisualSceneStateSystem.InjectToWorld(ref builder, realmData, scenesCache, lodAssetsPool, lodSettingsAsset, visualSceneStateResolver);
            UpdateSceneLODInfoSystem.InjectToWorld(ref builder, lodAssetsPool, lodSettingsAsset, memoryBudget,
                frameCapBudget, scenesCache, sceneReadinessReportQueue, lodContainer.transform, lodMaterialPool);
            UnloadSceneLODSystem.InjectToWorld(ref builder, lodAssetsPool, scenesCache);
            LODDebugToolsSystem.InjectToWorld(ref builder, debugBuilder, lodSettingsAsset, lodDebugContainer.transform);


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
        public StaticSettings.LODSettingsRef LODSettingAsset { get; set; }
        [field: Space]
        [field: SerializeField]
        public AssetReferenceMaterial lodMaterial { get; set; }
        
    }
}
