using Arch.Core;
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.DebugUtilities;
using DCL.DebugUtilities.UIBindings;
using DCL.Diagnostics;
using DCL.FeatureFlags;
using DCL.PluginSystem;
using DCL.PluginSystem.Global;
using DCL.Prefs;
using DCL.SceneRestrictionBusController.SceneRestrictionBus;
using DCL.SkyBox.Components;
using ECS;
using ECS.SceneLifeCycle;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Utility;
using Object = UnityEngine.Object;

namespace DCL.SkyBox
{
    public class SkyboxPlugin : IDCLGlobalPlugin<SkyboxPlugin.SkyboxTimeSettings>
    {
        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly Light directionalLight;
        private readonly IScenesCache scenesCache;
        private readonly ISceneRestrictionBusController sceneRestrictionController;
        private readonly IRealmData realmData;
        private readonly IDebugContainerBuilder debugBuilder;
        private readonly bool skyboxTimeEnabled;
        private readonly CancellationTokenSource debugLookCancellation = new ();

        private SkyboxSettings settingsJson;

        // One slot per debug entry, filled the first time that look is picked.
        private ProvidedAsset<SkyboxLookPreset>?[] debugLookPresets = Array.Empty<ProvidedAsset<SkyboxLookPreset>?>();

        private SkyboxSettingsAsset? skyboxSettings;
        private SkyboxRenderController? skyboxRenderController;

        public SkyboxPlugin(IAssetsProvisioner assetsProvisioner,
            Light directionalLight,
            IScenesCache scenesCache,
            ISceneRestrictionBusController sceneRestrictionController,
            IRealmData realmData,
            IDebugContainerBuilder debugBuilder,
            bool skyboxTimeEnabled = true)
        {
            this.assetsProvisioner = assetsProvisioner;
            this.directionalLight = directionalLight;
            this.scenesCache = scenesCache;
            this.sceneRestrictionController = sceneRestrictionController;
            this.realmData = realmData;
            this.debugBuilder = debugBuilder;
            this.skyboxTimeEnabled = skyboxTimeEnabled;
        }

        public void Dispose()
        {
            debugLookCancellation.SafeCancelAndDispose();

            for (var i = 0; i < debugLookPresets.Length; i++)
                debugLookPresets[i]?.Dispose();
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<World> builder, in GlobalPluginArguments arguments)
        {
            if (skyboxTimeEnabled)
                SkyboxTimeUpdateSystem.InjectToWorld(ref builder, skyboxSettings, scenesCache, sceneRestrictionController, skyboxRenderController, realmData, arguments.SkyboxEntity);
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

                SetInitialTime(settingsJson, skyboxSettings, skyboxTimeEnabled);

                skyboxRenderController = Object.Instantiate((await assetsProvisioner.ProvideMainAssetAsync(skyboxSettings.SkyboxRenderControllerPrefab, ct: ct)).Value);

                AnimationClip skyboxAnimation = (await assetsProvisioner.ProvideMainAssetAsync(skyboxSettings.SkyboxAnimationCycle, ct: ct)).Value;

                // Read the persisted quality setting. On first launch or when running concurrently with
                // QualitySettingsController init, this may not yet be written — ApplySunLensFlare will
                // correct the component's enabled state once QualitySettingsController finishes initializing.
                bool lensFlareEnabled = DCLPlayerPrefs.GetBool(DCLPrefKeys.PS_SUN_LENS_FLARE, defaultValue: true);

                skyboxRenderController.Initialize(
                    skyboxSettings.SkyboxMaterial,
                    directionalLight,
                    skyboxAnimation,
                    skyboxSettings.TimeOfDayNormalized,
                    lensFlareEnabled
                );

                if (!skyboxTimeEnabled)
                    skyboxRenderController.DisableSkyboxTime();

                AddLookPresetDebugWidget(skyboxRenderController, skyboxSettings);
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

            void SetInitialTime(SkyboxSettings jsonConfig, SkyboxSettingsAsset skyboxSettings, bool timeEnabled)
            {
                // When skybox time is disabled, pin the initial time to noon so it's independent of host
                // clock / PlayerPrefs / FF — the update system that would honor those isn't injected here.
                if (!timeEnabled)
                {
                    const float NOON = 0.5f;
                    skyboxSettings.TimeOfDayNormalized = NOON;
                    skyboxSettings.TargetTimeOfDayNormalized = NOON;
                    skyboxSettings.UIOverrideTimeOfDayNormalized = NOON;
                    skyboxSettings.IsUIControlled = true;
                    skyboxSettings.IsDayCycleEnabled = false;
                    return;
                }

                if (DCLPlayerPrefs.HasKey(DCLPrefKeys.SKYBOX_FIXED_TIME))
                {
                    float fixedTime = DCLPlayerPrefs.GetFloat(DCLPrefKeys.SKYBOX_FIXED_TIME);
                    skyboxSettings.TimeOfDayNormalized = fixedTime;
                    skyboxSettings.TargetTimeOfDayNormalized = fixedTime;
                    skyboxSettings.UIOverrideTimeOfDayNormalized = fixedTime;
                    // Force the state to not cycle, as it was previously assigned by the user
                    skyboxSettings.IsUIControlled = true;
                    skyboxSettings.IsDayCycleEnabled = false;
                }
                else
                {
                    if (jsonConfig.FixedTimeInSeconds != null)
                    {
                        float normalizedTime = SkyboxSettingsAsset.NormalizeTime(jsonConfig.FixedTimeInSeconds.Value);
                        skyboxSettings.TimeOfDayNormalized = normalizedTime;
                        skyboxSettings.TargetTimeOfDayNormalized = normalizedTime;
                        skyboxSettings.UIOverrideTimeOfDayNormalized = normalizedTime;
                        // Force the state to not cycle, as the time has been set by the feature flag
                        skyboxSettings.IsUIControlled = true;
                        skyboxSettings.IsDayCycleEnabled = false;
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

        /// <summary>
        ///     Debug dropdown to compare looks at runtime. Index 0 is the look the prefab ships with, already loaded;
        ///     the others come from the settings entries and are loaded the first time they are picked, so nothing
        ///     but the shipped look is resident until then.
        /// </summary>
        private void AddLookPresetDebugWidget(SkyboxRenderController controller, SkyboxSettingsAsset settings)
        {
            SkyboxSettingsAsset.LookPresetEntry[] entries = settings.DebugLookPresets;

            if (entries.Length == 0)
                return;

            SkyboxLookPreset shippedPreset = controller.Preset;
            debugLookPresets = new ProvidedAsset<SkyboxLookPreset>?[entries.Length];

            var names = new List<string>(entries.Length + 1) { shippedPreset.name };

            for (var i = 0; i < entries.Length; i++)
                names.Add(entries[i].Name);

            var binding = new IndexedElementBinding(names, shippedPreset.name, evt => SwitchLookAsync(controller, shippedPreset, entries, evt.index, debugLookCancellation.Token).Forget());

            debugBuilder.TryAddWidget(IDebugContainerBuilder.Categories.SKYBOX)
                       ?.AddControl(new DebugDropdownDef(binding, "Look preset"), null);
        }

        private async UniTaskVoid SwitchLookAsync(SkyboxRenderController controller, SkyboxLookPreset shippedPreset, SkyboxSettingsAsset.LookPresetEntry[] entries, int index, CancellationToken ct)
        {
            try
            {
                SkyboxLookPreset preset = shippedPreset;

                if (index > 0)
                {
                    ProvidedAsset<SkyboxLookPreset>? loaded = debugLookPresets[index - 1];

                    if (loaded == null)
                    {
                        loaded = await assetsProvisioner.ProvideMainAssetAsync(entries[index - 1].Preset, ct);
                        debugLookPresets[index - 1] = loaded;
                    }

                    preset = loaded.Value.Value;
                }

                controller.ApplyPreset(preset);
            }
            catch (OperationCanceledException) { }
            catch (Exception e) { ReportHub.LogException(e, ReportCategory.SKYBOX); }
        }

        [Serializable]
        public class SkyboxTimeSettings : IDCLPluginSettings
        {
            [field: SerializeField]
            public SkyboxSettingsAsset Settings { get; private set; }
        }

        [Serializable]
        private struct SkyboxSettings
        {
#pragma warning disable UAC1001 // Newtonsoft-only feature-flag payload (TryGetJsonPayload -> JsonConvert); Unity serialization never reads this struct.
            [JsonProperty("fixedTimeInSeconds")]
            public uint? FixedTimeInSeconds;
            [JsonProperty("dayCycleDurationInSeconds")]
            public uint? DayCycleDurationInSeconds;
#pragma warning restore UAC1001
        }
    }
}
