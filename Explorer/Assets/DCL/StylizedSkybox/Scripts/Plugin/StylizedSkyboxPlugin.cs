using Arch.Core;
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.DebugUtilities;
using DCL.FeatureFlags;
using DCL.PluginSystem;
using DCL.PluginSystem.Global;
using ECS.SceneLifeCycle;
using System;
using System.Threading;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DCL.StylizedSkybox.Scripts.Plugin
{
    public class StylizedSkyboxPlugin : IDCLGlobalPlugin<StylizedSkyboxPlugin.StylizedSkyboxSettings>
    {
        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly Light directionalLight;
        private readonly IDebugContainerBuilder debugContainerBuilder;
        private SkyboxController? skyboxController;
        private readonly FeatureFlagsCache featureFlagsCache;
        private readonly IScenesCache scenesCache;

        public StylizedSkyboxPlugin(
            IAssetsProvisioner assetsProvisioner,
            Light directionalLight,
            IDebugContainerBuilder debugContainerBuilder,
            FeatureFlagsCache featureFlagsCache,
            IScenesCache scenesCache
        )
        {
            this.assetsProvisioner = assetsProvisioner;
            this.directionalLight = directionalLight;
            this.debugContainerBuilder = debugContainerBuilder;
            this.featureFlagsCache = featureFlagsCache;
            this.scenesCache = scenesCache;
        }

        public void Dispose() { }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<World> builder, in GlobalPluginArguments arguments) { }

        public async UniTask InitializeAsync(StylizedSkyboxSettings settings, CancellationToken ct)
        {
            var settingsAsset = settings.SettingsAsset;

            skyboxController = Object.Instantiate((await assetsProvisioner.ProvideMainAssetAsync(settingsAsset.StylizedSkyboxPrefab, ct: ct)).Value.GetComponent<SkyboxController>());
            AnimationClip skyboxAnimation = (await assetsProvisioner.ProvideMainAssetAsync(settingsAsset.SkyboxAnimationCycle, ct: ct)).Value;

            skyboxController.Initialize(settingsAsset.SkyboxMaterial,
                directionalLight,
                skyboxAnimation,
                featureFlagsCache,
                settingsAsset,
                scenesCache,
                debugContainerBuilder);
        }

        [Serializable]
        public class StylizedSkyboxSettings : IDCLPluginSettings
        {
            public StylizedSkyboxSettingsAsset SettingsAsset;
        }
    }
}
