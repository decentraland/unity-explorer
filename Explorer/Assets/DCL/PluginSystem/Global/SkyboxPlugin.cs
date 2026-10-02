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
using ECS;
using ECS.SceneLifeCycle;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;
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
        private CancellationTokenSource debugLookCancellation = new ();

        private SkyboxSettings settingsJson;

        // One slot per settings entry, filled the first time that look is needed (feature flag or debug pick).
        private SkyboxSettingsAsset.LookPresetEntry[] lookEntries = Array.Empty<SkyboxSettingsAsset.LookPresetEntry>();
        private ProvidedAsset<SkyboxLookPreset>?[] lookPresets = Array.Empty<ProvidedAsset<SkyboxLookPreset>?>();

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

            for (var i = 0; i < lookPresets.Length; i++)
            {
                // A load cancelled before it reached its slot still holds the reference's handle.
                if (lookPresets[i] is { } provided)
                    provided.Dispose();
                else if (lookEntries[i].Preset.OperationHandle.IsValid())
                    lookEntries[i].Preset.ReleaseAsset();
            }
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

                lookEntries = skyboxSettings.LookPresets;
                lookPresets = new ProvidedAsset<SkyboxLookPreset>?[lookEntries.Length];
                SkyboxLookPreset defaultPreset = skyboxRenderController.Preset;

                // Before Initialize, so fog, statics and lens flare are set up from the flagged look.
                if (await LoadFlaggedLookPresetAsync(skyboxSettings, ct) is { } flaggedPreset)
                    skyboxRenderController.ApplyPreset(flaggedPreset);

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

                AddLookPresetDebugWidget(skyboxRenderController, defaultPreset);
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
        ///     The look named by the feature flag payload, or null to keep the prefab's default look: no flag, no payload,
        ///     a name missing from the settings entries, or a failed load.
        /// </summary>
        private async UniTask<SkyboxLookPreset?> LoadFlaggedLookPresetAsync(SkyboxSettingsAsset settings, CancellationToken ct)
        {
            if (!FeatureFlagsConfiguration.Instance.TryGetTextPayload(FeatureFlagsStrings.SKYBOX_LOOK_PRESET, FeatureFlagsStrings.SKYBOX_LOOK_PRESET_VARIANT, out string? presetName)
                || string.IsNullOrWhiteSpace(presetName))
                return null;

            int index = settings.IndexOfLookPreset(presetName);

            if (index < 0)
            {
                ReportHub.LogWarning(ReportCategory.SKYBOX, $"Skybox look preset \"{presetName}\" from the feature flag is not in the settings look presets, keeping the default look");
                return null;
            }

            try { return await LoadLookPresetAsync(index, ct); }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                ReportHub.LogException(new Exception($"Skybox look preset \"{presetName}\" from the feature flag failed to load, keeping the default look", e), ReportCategory.SKYBOX);
                return null;
            }
        }

        /// <summary>
        ///     Debug dropdown to compare looks at runtime. Index 0 is the prefab's default look, already loaded; the others
        ///     come from the settings entries and are loaded the first time they are needed, so only the active look is
        ///     resident until then. The current selection is the active look, which the feature flag may have changed.
        /// </summary>
        private void AddLookPresetDebugWidget(SkyboxRenderController controller, SkyboxLookPreset defaultPreset)
        {
            if (lookEntries.Length == 0)
                return;

            var names = new List<string>(lookEntries.Length + 1) { defaultPreset.name };

            for (var i = 0; i < lookEntries.Length; i++)
                names.Add(lookEntries[i].Name);

            string activeName = names[0];

            for (var i = 0; i < lookPresets.Length; i++)
                if (lookPresets[i] is { } provided && provided.Value == controller.Preset)
                    activeName = names[i + 1];

            var binding = new IndexedElementBinding(names, activeName, evt =>
            {
                // A newer pick cancels the previous load so a late one cannot apply over it.
                debugLookCancellation = debugLookCancellation.SafeRestart();
                SwitchLookAsync(controller, defaultPreset, evt.index, debugLookCancellation.Token).Forget();
            });

            debugBuilder.TryAddWidget(IDebugContainerBuilder.Categories.SKYBOX)
                       ?.AddControl(new DebugDropdownDef(binding, "Look preset"), null);
        }

        private async UniTaskVoid SwitchLookAsync(SkyboxRenderController controller, SkyboxLookPreset defaultPreset, int index, CancellationToken ct)
        {
            try
            {
                SkyboxLookPreset preset = index > 0 ? await LoadLookPresetAsync(index - 1, ct) : defaultPreset;
                controller.ApplyPreset(preset);
            }
            catch (OperationCanceledException) { }
            catch (Exception e) { ReportHub.LogException(e, ReportCategory.SKYBOX); }
        }

        private async UniTask<SkyboxLookPreset> LoadLookPresetAsync(int entryIndex, CancellationToken ct)
        {
            ProvidedAsset<SkyboxLookPreset>? loaded = lookPresets[entryIndex];

            if (loaded == null)
            {
                AssetReferenceT<SkyboxLookPreset> reference = lookEntries[entryIndex].Preset;

                // A load cancelled by an earlier pick keeps running on the reference, and the provisioner hands that
                // handle back unfinished; waiting on it first means the provided asset is always complete.
                if (reference.OperationHandle.IsValid())
                    await reference.OperationHandle.WithCancellation(ct);

                loaded = await assetsProvisioner.ProvideMainAssetAsync(reference, ct);
                lookPresets[entryIndex] = loaded;
            }

            return loaded.Value.Value;
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
