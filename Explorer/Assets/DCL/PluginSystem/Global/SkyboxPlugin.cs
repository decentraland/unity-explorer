using Arch.Core;
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Diagnostics;
using DCL.FeatureFlags;
using DCL.PluginSystem;
using DCL.PluginSystem.Global;
using DCL.Prefs;
using DCL.SceneRestrictionBusController.SceneRestrictionBus;
using ECS.SceneLifeCycle;
using Newtonsoft.Json;
using System;
using System.Threading;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DCL.SkyBox
{
    public class SkyboxPlugin : IDCLGlobalPlugin<SkyboxPlugin.SkyboxTimeSettings>
    {
        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly Light directionalLight;
        private readonly IScenesCache scenesCache;
        private readonly ISceneRestrictionBusController sceneRestrictionController;

        private SkyboxSettings settingsJson;

        private SkyboxSettingsAsset? skyboxSettings;
        private SkyboxRenderController? skyboxRenderController;

        public SkyboxPlugin(IAssetsProvisioner assetsProvisioner,
            Light directionalLight,
            IScenesCache scenesCache,
            ISceneRestrictionBusController sceneRestrictionController)
        {
            this.assetsProvisioner = assetsProvisioner;
            this.directionalLight = directionalLight;
            this.scenesCache = scenesCache;
            this.sceneRestrictionController = sceneRestrictionController;
        }

        public void Dispose() { }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<World> builder, in GlobalPluginArguments arguments)
        {
            SkyboxTimeUpdateSystem.InjectToWorld(ref builder, skyboxSettings, scenesCache, sceneRestrictionController, skyboxRenderController, arguments.SkyboxEntity);
        }

        public async UniTask InitializeAsync(SkyboxTimeSettings pluginSettings, CancellationToken ct)
        {
            try
            {
                skyboxSettings = pluginSettings.Settings;
                skyboxSettings.Reset();

                if (FeatureFlagsConfiguration.Instance.TryGetJsonPayload(FeatureFlagsStrings.SKYBOX_SETTINGS, FeatureFlagsStrings.SKYBOX_SETTINGS_VARIANT, out settingsJson))
                    if (settingsJson.DayCycleDurationInSeconds != null)
                        skyboxSettings.FullDayCycleInSeconds =  settingsJson.DayCycleDurationInSeconds.Value;

                SetInitialTime(settingsJson, skyboxSettings);

                skyboxRenderController = Object.Instantiate((await assetsProvisioner.ProvideMainAssetAsync(skyboxSettings.SkyboxRenderControllerPrefab, ct: ct)).Value);

                AnimationClip skyboxAnimation = (await assetsProvisioner.ProvideMainAssetAsync(skyboxSettings.SkyboxAnimationCycle, ct: ct)).Value;

                skyboxRenderController.Initialize(
                    skyboxSettings.SkyboxMaterial,
                    directionalLight,
                    skyboxAnimation,
                    skyboxSettings.TimeOfDayNormalized
                );
            }
            catch (OperationCanceledException)
            {
                // Ignore cancellation
            }
            catch (Exception ex)
            {
                ReportHub.LogError(ReportCategory.SKYBOX, $"Failed to initialize SkyboxPlugin: {ex}");
                throw;
            }

            return;

            void SetInitialTime(SkyboxSettings jsonConfig, SkyboxSettingsAsset skyboxSettings)
            {
                if (DCLPlayerPrefs.HasKey(DCLPrefKeys.SKYBOX_FIXED_TIME))
                {
                    float fixedTime = DCLPlayerPrefs.GetFloat(DCLPrefKeys.SKYBOX_FIXED_TIME);
                    skyboxSettings.TimeOfDayNormalized = fixedTime;
                    skyboxSettings.TargetTimeOfDayNormalized = fixedTime;
                    // Force the state to not cycle, as it was previously assigned by the user
                    skyboxSettings.IsUIControlled = true;
                }
                else
                {
                    if (jsonConfig.FixedTimeInSeconds != null)
                    {
                        float normalizedTime = SkyboxSettingsAsset.NormalizeTime(jsonConfig.FixedTimeInSeconds.Value);
                        skyboxSettings.TimeOfDayNormalized = normalizedTime;
                        skyboxSettings.TargetTimeOfDayNormalized = normalizedTime;
                        // Force the state to not cycle, as the time has been set by the feature flag
                        skyboxSettings.IsUIControlled = true;
                    }
                    else
                    {
                        float globalTime = skyboxSettings.GlobalTimeOfDayNormalized;
                        skyboxSettings.TimeOfDayNormalized = globalTime;
                        skyboxSettings.TargetTimeOfDayNormalized = globalTime;
                    }
                }
            }
        }

        public class SkyboxTimeSettings : IDCLPluginSettings
        {
            [field: SerializeField]
            public SkyboxSettingsAsset Settings { get; private set; }
        }

        [Serializable]
        private struct SkyboxSettings
        {
            [JsonProperty("fixedTimeInSeconds")]
            public uint? FixedTimeInSeconds;
            [JsonProperty("dayCycleDurationInSeconds")]
            public uint? DayCycleDurationInSeconds;
        }
    }
}
