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
        private readonly ElementBinding<int> timeOfDay;
        private readonly FeatureFlagsCache featureFlagsCache;

        public StylizedSkyboxPlugin(
            IAssetsProvisioner assetsProvisioner,
            Light directionalLight,
            IDebugContainerBuilder debugContainerBuilder,
            FeatureFlagsCache featureFlagsCache
        )
        {
            timeOfDay = new ElementBinding<int>(0);
            this.assetsProvisioner = assetsProvisioner;
            this.directionalLight = directionalLight;
            this.debugContainerBuilder = debugContainerBuilder;
            this.featureFlagsCache = featureFlagsCache;
        }

        public void Dispose() { }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<World> builder, in GlobalPluginArguments arguments) { }

        public async UniTask InitializeAsync(StylizedSkyboxSettings settings, CancellationToken ct)
        {
            var settingsAsset = settings.SettingsAsset;

            skyboxController = Object.Instantiate((await assetsProvisioner.ProvideMainAssetAsync(settingsAsset.StylizedSkyboxPrefab, ct: ct)).Value.GetComponent<SkyboxController>());
            AnimationClip skyboxAnimation = (await assetsProvisioner.ProvideMainAssetAsync(settingsAsset.SkyboxAnimationCycle, ct: ct)).Value;

            settingsAsset.TimeOfDay = (int)(skyboxController!.NaturalTime / (skyboxController!.SecondsInDay / 24f));
            settingsAsset.TimeOfDayChanged += OnTimeOfDayChanged;

            skyboxController.Initialize(settingsAsset.SkyboxMaterial, directionalLight, skyboxAnimation, featureFlagsCache);

            debugContainerBuilder.TryAddWidget("Skybox")
                                 ?.AddSingleButton("Play", () => skyboxController.Play())
                                 .AddSingleButton("Pause", () => skyboxController.Pause())
                                 .AddIntSliderField("Time", timeOfDay, 0, skyboxController.SecondsInDay)
                                 .AddSingleButton("SetTime", () => skyboxController.SetTime(timeOfDay.Value)); //TODO: replace this by a system to update the value
        }

        private void OnTimeOfDayChanged(int hour)
        {
            int seconds = skyboxController!.SecondsInDay / 24 * hour;

            timeOfDay.Value = seconds;
            skyboxController.Pause();
            skyboxController.SetTime(seconds);
        }

        [Serializable]
        public class StylizedSkyboxSettings : IDCLPluginSettings
        {
            public StylizedSkyboxSettingsAsset SettingsAsset;
        }
    }
}
