using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Optimization.Pools;
using DCL.PluginSystem.Global;
using DCL.PluginSystem.World.Dependencies;
using DCL.ResourcesUnloading;
using ECS.LifeCycle;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DCL.PluginSystem.World
{
    public class MainPlayerTriggerAreaPlugin : IDCLWorldPlugin<MainPlayerTriggerAreaSettings>
    {
        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly CacheCleaner cacheCleaner;
        private readonly IComponentPoolsRegistry componentPoolsRegistry;

        private IComponentPool<MainPlayerTriggerArea.MainPlayerTriggerArea> mainPlayerTriggerAreaPoolRegistry;

        public MainPlayerTriggerAreaPlugin(IComponentPoolsRegistry poolsRegistry, IAssetsProvisioner assetsProvisioner, CacheCleaner cacheCleaner)
        {
            this.assetsProvisioner = assetsProvisioner;
            componentPoolsRegistry = poolsRegistry;
            this.cacheCleaner = cacheCleaner;
        }

        public void Dispose()
        {
            //ignore
            mainPlayerTriggerAreaPoolRegistry.Dispose();
        }

        public async UniTask InitializeAsync(MainPlayerTriggerAreaSettings settings, CancellationToken ct)
        {
            await CreateMainPlayerTriggerAreaPoolAsync(settings, ct);
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in ECSWorldInstanceSharedDependencies sharedDependencies, in PersistentEntities persistentEntities, List<IFinalizeWorldSystem> finalizeWorldSystems)
        {
            // cacheCleaner.Register(mainPlayerTriggerAreaPoolRegistry);
        }

        public void InjectToEmptySceneWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in EmptyScenesWorldSharedDependencies dependencies) { }

        private async UniTask CreateMainPlayerTriggerAreaPoolAsync(MainPlayerTriggerAreaSettings settings, CancellationToken ct)
        {
            MainPlayerTriggerArea.MainPlayerTriggerArea mainPlayerTriggerAreaPrefab = (await assetsProvisioner.ProvideMainAssetAsync(settings.MainPlayerTriggerAreaPrefab, ct: ct)).Value.GetComponent<MainPlayerTriggerArea.MainPlayerTriggerArea>();

            // var parentContainer = new GameObject("MainPlayerTriggerAreaPool");
            // componentPoolsRegistry.AddGameObjectPool(() => Object.Instantiate(mainPlayerTriggerArea, parentContainer.transform));
            componentPoolsRegistry.AddGameObjectPool(() => Object.Instantiate(mainPlayerTriggerAreaPrefab, Vector3.zero, Quaternion.identity));
            mainPlayerTriggerAreaPoolRegistry = componentPoolsRegistry.GetReferenceTypePool<MainPlayerTriggerArea.MainPlayerTriggerArea>();

            cacheCleaner.Register(mainPlayerTriggerAreaPoolRegistry);
        }
    }
}
