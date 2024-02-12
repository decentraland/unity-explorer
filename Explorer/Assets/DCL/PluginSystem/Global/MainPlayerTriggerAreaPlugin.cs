using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Optimization.Pools;
using DCL.ResourcesUnloading;
using DCL.SDKComponents.CameraModeArea;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Serialization;
using Object = UnityEngine.Object;

namespace DCL.PluginSystem.Global
{
    public class MainPlayerTriggerAreaPlugin : IDCLGlobalPlugin<MainPlayerTriggerAreaPlugin.MainPlayerTriggerAreaSettings>
    {
        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly CacheCleaner cacheCleaner;
        private readonly IComponentPoolsRegistry componentPoolsRegistry;

        private IComponentPool<MainPlayerTriggerArea> mainPlayerTriggerAreaPoolRegistry;

        public MainPlayerTriggerAreaPlugin(IComponentPoolsRegistry poolsRegistry, IAssetsProvisioner assetsProvisioner, CacheCleaner cacheCleaner)
        {
            this.assetsProvisioner = assetsProvisioner;
            componentPoolsRegistry = poolsRegistry;
            this.cacheCleaner = cacheCleaner;
        }

        public void Dispose() { }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
            cacheCleaner.Register(mainPlayerTriggerAreaPoolRegistry);
        }

        public async UniTask InitializeAsync(MainPlayerTriggerAreaSettings settings, CancellationToken ct)
        {
            await CreateMainPlayerTriggerAreaPoolAsync(settings, ct);
        }

        private async UniTask CreateMainPlayerTriggerAreaPoolAsync(MainPlayerTriggerAreaSettings settings, CancellationToken ct)
        {
            MainPlayerTriggerArea mainPlayerTriggerAreaPrefab = (await assetsProvisioner.ProvideMainAssetAsync(settings.MainPlayerTriggerAreaPrefab, ct: ct)).Value.GetComponent<MainPlayerTriggerArea>();

            // var parentContainer = new GameObject("MainPlayerTriggerAreaPool");
            // componentPoolsRegistry.AddGameObjectPool(() => Object.Instantiate(mainPlayerTriggerArea, parentContainer.transform));
            componentPoolsRegistry.AddGameObjectPool(() => Object.Instantiate(mainPlayerTriggerAreaPrefab, Vector3.zero, Quaternion.identity));
            mainPlayerTriggerAreaPoolRegistry = componentPoolsRegistry.GetReferenceTypePool<MainPlayerTriggerArea>();
        }

        [Serializable]
        public class MainPlayerTriggerAreaSettings : IDCLPluginSettings
        {
            [FormerlySerializedAs("MainPlayerTriggerAreaGO")]
            [FormerlySerializedAs("mainPlayerTriggerAreaGO")]
            [field: Header(nameof(MainPlayerTriggerAreaPlugin) + "." + nameof(MainPlayerTriggerAreaSettings))]
            [field: Space]
            [field: SerializeField]
            public AssetReferenceGameObject MainPlayerTriggerAreaPrefab;
        }

        // [Serializable]
        // public class MainPlayerTriggerAreaReference : ComponentReference<AssetReferenceGameObject>
        // {
        //     public MainPlayerTriggerAreaReference(string guid) : base(guid) { }
        // }
    }
}
