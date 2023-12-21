using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Landscape;
using DCL.Landscape.Config;
using DCL.Landscape.Settings;
using DCL.Landscape.Systems;
using DCL.PluginSystem.World;
using DCL.PluginSystem.World.Dependencies;
using ECS.LifeCycle;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace DCL.PluginSystem.Global
{
    public class LandscapePlugin : IDCLWorldPlugin<LandscapeSettings>
    {
        private readonly IAssetsProvisioner assetsProvisioner;
        private ProvidedAsset<LandscapeData> landscapeData;
        private readonly LandscapeAssetPoolManager poolManager;

        public LandscapePlugin(IAssetsProvisioner assetsProvisioner)
        {
            this.assetsProvisioner = assetsProvisioner;
            poolManager = new LandscapeAssetPoolManager();
        }

        public async UniTask InitializeAsync(LandscapeSettings settings, CancellationToken ct)
        {
            landscapeData = await assetsProvisioner.ProvideMainAssetAsync(settings.landscapeData, ct);

            foreach (LandscapeAsset landscapeAsset in landscapeData.Value.assets) { poolManager.Add(landscapeAsset.asset, landscapeAsset.poolPreWarmCount); }
        }

        public void Dispose()
        {
            landscapeData.Dispose();
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in ECSWorldInstanceSharedDependencies sharedDependencies, in PersistentEntities persistentEntities, List<IFinalizeWorldSystem> finalizeWorldSystems) { }

        public void InjectToEmptySceneWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in EmptyScenesWorldSharedDependencies dependencies)
        {
            Debug.Log("Injecting Landscape Systems");
            LandscapeParcelInitializerSystem.InjectToWorld(ref builder, landscapeData.Value, poolManager);
            LandscapeParcelUnloadSystem.InjectToWorld(ref builder, poolManager);
        }
    }
}
