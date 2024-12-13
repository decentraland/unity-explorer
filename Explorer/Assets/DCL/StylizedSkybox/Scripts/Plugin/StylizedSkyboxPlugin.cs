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

        private StylizedSkyboxSettingsAsset? settingsAsset;

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
            settingsAsset = settings.SettingsAsset;

            skyboxController = Object.Instantiate((await assetsProvisioner.ProvideMainAssetAsync(settingsAsset.StylizedSkyboxPrefab, ct: ct)).Value.GetComponent<SkyboxController>());
            AnimationClip skyboxAnimation = (await assetsProvisioner.ProvideMainAssetAsync(settingsAsset.SkyboxAnimationCycle, ct: ct)).Value;

            skyboxController.Initialize(settingsAsset.SkyboxMaterial, directionalLight, skyboxAnimation, featureFlagsCache);

            settingsAsset.TimeOfDay = (int)(skyboxController!.NaturalTime / (skyboxController!.SecondsInDay / 24f));
            settingsAsset.Speed = StylizedSkyboxSettingsAsset.TimeProgression.Default;
            settingsAsset.TimeOfDayChanged += OnTimeOfDayChanged;
            settingsAsset.SpeedChanged += OnSpeedChanged;

            debugContainerBuilder.TryAddWidget("Skybox")
                                ?.AddSingleButton("Play", () => skyboxController.Paused = false)
                                 .AddSingleButton("Pause", () => skyboxController.Paused = true)
                                 .AddIntSliderField("Time", timeOfDay, 0, skyboxController.SecondsInDay)
                                 .AddSingleButton("SetTime", () => skyboxController.SetTime(timeOfDay.Value)); //TODO: replace this by a system to update the value
        }

        private void OnSpeedChanged(StylizedSkyboxSettingsAsset.TimeProgression speed)
        {
            skyboxController!.Speed =
                speed switch
                {
                    StylizedSkyboxSettingsAsset.TimeProgression.Paused => 0,
                    StylizedSkyboxSettingsAsset.TimeProgression.Default => skyboxController.DefaultSpeed,
                    StylizedSkyboxSettingsAsset.TimeProgression.Fast => 600,
                    StylizedSkyboxSettingsAsset.TimeProgression.VeryFast => 3600,
                    _ => throw new ArgumentOutOfRangeException(nameof(speed), speed, null),
                };
        }

        private void OnTimeOfDayChanged(int hour)
        {
            int seconds = skyboxController!.SecondsInDay / 24 * hour;

            timeOfDay.Value = seconds;
            skyboxController.SetTime(seconds);
        }

        [Serializable]
        public class StylizedSkyboxSettings : IDCLPluginSettings
        {
            public StylizedSkyboxSettingsAsset SettingsAsset;
        }
    }
}
