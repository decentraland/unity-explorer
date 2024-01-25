using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Arch.Core;
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.DebugUtilities;
using DCL.LOD.Systems;
using DCL.Optimization.PerformanceBudgeting;
using DCL.PluginSystem;
using DCL.PluginSystem.Global;
using DCL.ResourcesUnloading;
using ECS;
using ECS.Prioritization;
using ECS.SceneLifeCycle;
using ECS.SceneLifeCycle.Systems;
using UnityEngine;

namespace DCL.LOD
{
    public class LODPlugin : IDCLGlobalPlugin<LODSettings>
    {
        private ProvidedAsset<LODSettingsAsset> lodSettingsAsset;
        private readonly IAssetsProvisioner assetsProvisioner;

        private readonly LODAssetsPool lodAssetsPool;
        private readonly IScenesCache scenesCache;
        private readonly IRealmData realmData;
        private readonly IPerformanceBudget frameCapBudget;
        private readonly IPerformanceBudget memoryBudget;
        private readonly IDebugContainerBuilder debugBuilder;

        public LODPlugin(CacheCleaner cacheCleaner, RealmData realmData, IPerformanceBudget memoryBudget,
            IPerformanceBudget frameCapBudget, IScenesCache scenesCache, IDebugContainerBuilder debugBuilder, IAssetsProvisioner assetsProvisioner)
        {
            lodAssetsPool = new LODAssetsPool();
            cacheCleaner.Register(lodAssetsPool);

            this.realmData = realmData;
            this.memoryBudget = memoryBudget;
            this.frameCapBudget = frameCapBudget;
            this.scenesCache = scenesCache;
            this.debugBuilder = debugBuilder;
            this.assetsProvisioner = assetsProvisioner;
        }

        public async UniTask InitializeAsync(LODSettings settings, CancellationToken ct)
        {
            lodSettingsAsset = await assetsProvisioner.ProvideMainAssetAsync(settings.LodSettingAsset, ct: ct);
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<World> builder, in GlobalPluginArguments arguments)
        {
            ResolveVisualSceneStateSystem.InjectToWorld(ref builder, lodSettingsAsset);
            UpdateVisualSceneStateSystem.InjectToWorld(ref builder, realmData, scenesCache, lodAssetsPool);
            ResolveSceneLODInfo.InjectToWorld(ref builder, lodAssetsPool);

            UpdateSceneLODInfoSystem.InjectToWorld(ref builder, lodAssetsPool, lodSettingsAsset, memoryBudget, frameCapBudget);

            UnloadSceneLODSystem.InjectToWorld(ref builder, lodAssetsPool);

            LODDebugToolsSystem.InjectToWorld(ref builder, debugBuilder, lodSettingsAsset);
        }

        public void Dispose()
        {
            // TODO release managed resources here
        }
    }

    [Serializable]
    public class LODSettings : IDCLPluginSettings
    {
        [field: Header(nameof(LODPlugin) + "." + nameof(LODSettings))]
        [field: Space]
        [field: SerializeField]
        public StaticSettings.LODSettingsRef LodSettingAsset { get; set; }
    }
}
