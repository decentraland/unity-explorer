using Arch.Core;
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.DebugUtilities;
using DCL.DebugUtilities.UIBindings;
using DCL.FeatureFlags;
using DCL.PluginSystem;
using DCL.PluginSystem.Global;
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
        private readonly ElementBinding<float> timeOfDay;

        private StylizedSkyboxSettingsAsset? settingsAsset;

        public StylizedSkyboxPlugin(
            IAssetsProvisioner assetsProvisioner,
            Light directionalLight,
            IDebugContainerBuilder debugContainerBuilder)
        {
            timeOfDay = new ElementBinding<float>(0);
            this.assetsProvisioner = assetsProvisioner;
            this.directionalLight = directionalLight;
            this.debugContainerBuilder = debugContainerBuilder;
        }

        public void Dispose() { }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<World> builder, in GlobalPluginArguments arguments) { }

        public async UniTask InitializeAsync(StylizedSkyboxSettings settings, CancellationToken ct)
        {
            settingsAsset = settings.SettingsAsset;

            skyboxController = Object.Instantiate((await assetsProvisioner.ProvideMainAssetAsync(settingsAsset.StylizedSkyboxPrefab, ct: ct)).Value.GetComponent<SkyboxController>());
            AnimationClip skyboxAnimation = (await assetsProvisioner.ProvideMainAssetAsync(settingsAsset.SkyboxAnimationCycle, ct: ct)).Value;

            skyboxController.Initialize(settingsAsset.SkyboxMaterial, directionalLight, skyboxAnimation, settingsAsset, FeatureFlagsConfiguration.Instance);

            debugContainerBuilder.TryAddWidget("Skybox")
                                ?.AddSingleButton("Play", () => skyboxController.UseDynamicTime = true)
                                 .AddSingleButton("Pause", () => skyboxController.UseDynamicTime = false)
                                 .AddFloatSliderField("Time", timeOfDay, 0, 1)
                                 .AddSingleButton("SetTime", () => skyboxController.SetTimeOverride(timeOfDay.Value)); //TODO: replace this by a system to update the value
        }

        [Serializable]
        public class StylizedSkyboxSettings : IDCLPluginSettings
        {
            public StylizedSkyboxSettingsAsset SettingsAsset;
        }
    }
}
