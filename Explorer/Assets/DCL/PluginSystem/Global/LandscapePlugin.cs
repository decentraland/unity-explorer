using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Landscape.Settings;
using DCL.Landscape.Systems;
using DCL.PluginSystem.World;
using DCL.PluginSystem.World.Dependencies;
using ECS.LifeCycle;
using System;
using System.Collections.Generic;
using System.Threading;

namespace DCL.PluginSystem.Global
{
    public class LandscapePlugin : IDCLWorldPlugin<LandscapeSettings>
    {
        private readonly IAssetsProvisioner assetsProvisioner;
        private ProvidedAsset<LandscapeData> landscapeData;

        public LandscapePlugin(IAssetsProvisioner assetsProvisioner)
        {
            this.assetsProvisioner = assetsProvisioner;
        }

        public async UniTask InitializeAsync(LandscapeSettings settings, CancellationToken ct)
        {
            landscapeData = await assetsProvisioner.ProvideMainAssetAsync(settings.landscapeData, ct);
        }

        public void Dispose()
        {
            landscapeData.Dispose();
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in ECSWorldInstanceSharedDependencies sharedDependencies, in PersistentEntities persistentEntities, List<IFinalizeWorldSystem> finalizeWorldSystems) { }

        public void InjectToEmptySceneWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in EmptyScenesWorldSharedDependencies dependencies)
        {
            LandscapeParcelInitializerSystem.InjectToWorld(ref builder, landscapeData.Value);
        }
    }
}
