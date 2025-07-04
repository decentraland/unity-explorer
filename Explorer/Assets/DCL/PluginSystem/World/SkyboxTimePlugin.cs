using Arch.Core;
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.PluginSystem;
using DCL.PluginSystem.World;
using DCL.PluginSystem.World.Dependencies;
using DCL.SceneRestrictionBusController.SceneRestrictionBus;
using DCL.SDKComponents.SkyboxTime.Systems;
using DCL.SkyBox;
using ECS.LifeCycle;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace DCL.SDKComponents.SkyboxTime
{
    public class SkyboxTimePlugin : IDCLWorldPlugin<SkyboxTimePlugin.SkyboxTimeSettings>
    {
        private readonly ISceneRestrictionBusController sceneRestrictionController;
        private readonly IAssetsProvisioner assetsProvisioner;
        private SkyboxSettingsAsset skyboxSettings;

        public SkyboxTimePlugin(ISceneRestrictionBusController sceneRestrictionController, IAssetsProvisioner assetsProvisioner)
        {
            this.sceneRestrictionController = sceneRestrictionController;
            this.assetsProvisioner = assetsProvisioner;
        }

        public void Dispose() { }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<World> builder, in ECSWorldInstanceSharedDependencies sharedDependencies, in PersistentEntities persistentEntities, List<IFinalizeWorldSystem> finalizeWorldSystems, List<ISceneIsCurrentListener> sceneIsCurrentListeners)
        {

            var system = SkyboxTimeHandlerSystem.InjectToWorld(
                ref builder,
                skyboxSettings,
                persistentEntities.SceneRoot,
                sharedDependencies.SceneStateProvider,
                sceneRestrictionController);

            sceneIsCurrentListeners.Add(system);
        }

        public async UniTask InitializeAsync(SkyboxTimeSettings pluginSettings, CancellationToken ct)
        {
            ProvidedAsset<SkyboxSettingsAsset> skyboxSettingsAsset = await assetsProvisioner.ProvideMainAssetAsync(pluginSettings.Settings, ct);
            this.skyboxSettings = skyboxSettingsAsset.Value;
        }

        public class SkyboxTimeSettings : IDCLPluginSettings
        {
            [field: SerializeField]
            public AssetReferenceT<SkyboxSettingsAsset> Settings { get; private set; }
        }
    }
}
