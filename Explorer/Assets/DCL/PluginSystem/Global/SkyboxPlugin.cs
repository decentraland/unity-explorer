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
using DCL.Quality;
using DCL.SceneRestrictionBusController.SceneRestrictionBus;
using DCL.SkyBox.Components;
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
        private readonly IRendererFeaturesCache rendererFeaturesCache;
        private readonly IDebugContainerBuilder debugBuilder;
        private readonly bool skyboxTimeEnabled;
        private CancellationTokenSource debugLookCancellation = new ();

        private SkyboxSettings settingsJson;

        // One slot per debug entry, filled the first time that look is picked.
        private SkyboxSettingsAsset.LookPresetEntry[] debugLookEntries = Array.Empty<SkyboxSettingsAsset.LookPresetEntry>();
        private ProvidedAsset<SkyboxLookPreset>?[] debugLookPresets = Array.Empty<ProvidedAsset<SkyboxLookPreset>?>();

        private SkyboxSettingsAsset? skyboxSettings;
        private SkyboxRenderController? skyboxRenderController;

        public SkyboxPlugin(IAssetsProvisioner assetsProvisioner,
            Light directionalLight,
            IScenesCache scenesCache,
            ISceneRestrictionBusController sceneRestrictionController,
            IRealmData realmData,
            IRendererFeaturesCache rendererFeaturesCache,
            IDebugContainerBuilder debugBuilder,
            bool skyboxTimeEnabled = true)
        {
            this.assetsProvisioner = assetsProvisioner;
            this.directionalLight = directionalLight;
            this.scenesCache = scenesCache;
            this.sceneRestrictionController = sceneRestrictionController;
            this.realmData = realmData;
            this.rendererFeaturesCache = rendererFeaturesCache;
            this.debugBuilder = debugBuilder;
            this.skyboxTimeEnabled = skyboxTimeEnabled;
        }

        public void Dispose()
        {
            debugLookCancellation.SafeCancelAndDispose();

            for (var i = 0; i < debugLookPresets.Length; i++)
            {
                // A load cancelled before it reached its slot still holds the reference's handle.
                if (debugLookPresets[i] is { } provided)
                    provided.Dispose();
                else if (debugLookEntries[i].Preset.OperationHandle.IsValid())
                    debugLookEntries[i].Preset.ReleaseAsset();
            }
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<World> builder, in GlobalPluginArguments arguments)
        {
            // Tags the skybox entity so scene worlds can locate it and write their overrides
            builder.World.Add(arguments.SkyboxEntity, new SceneSkyboxOverrides());
            ApplySceneSkyboxOverridesSystem.InjectToWorld(ref builder, rendererFeaturesCache, skyboxRenderController, arguments.SkyboxEntity);

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
                    skyboxSettings.PanoramicSkyboxMaterial,
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

            void SetInitialTime(SkyboxSettings jsonConfig, SkyboxSettingsAsset settings, bool timeEnabled)
            {
                // When skybox time is disabled, pin the initial time to noon so it's independent of host
                // clock / PlayerPrefs / FF — the update system that would honor those isn't injected here.
                if (!timeEnabled)
                {
                    const float NOON = 0.5f;
                    settings.TimeOfDayNormalized = NOON;
                    settings.TargetTimeOfDayNormalized = NOON;
                    settings.UIOverrideTimeOfDayNormalized = NOON;
                    settings.IsUIControlled = true;
                    settings.IsDayCycleEnabled = false;
                    return;
                }

                if (DCLPlayerPrefs.HasKey(DCLPrefKeys.SKYBOX_FIXED_TIME))
                {
                    float fixedTime = DCLPlayerPrefs.GetFloat(DCLPrefKeys.SKYBOX_FIXED_TIME);
                    settings.TimeOfDayNormalized = fixedTime;
                    settings.TargetTimeOfDayNormalized = fixedTime;
                    settings.UIOverrideTimeOfDayNormalized = fixedTime;
                    // Force the state to not cycle, as it was previously assigned by the user
                    settings.IsUIControlled = true;
                    settings.IsDayCycleEnabled = false;
                }
                else
                {
                    if (jsonConfig.FixedTimeInSeconds != null)
                    {
                        float normalizedTime = SkyboxSettingsAsset.NormalizeTime(jsonConfig.FixedTimeInSeconds.Value);
                        settings.TimeOfDayNormalized = normalizedTime;
                        settings.TargetTimeOfDayNormalized = normalizedTime;
                        settings.UIOverrideTimeOfDayNormalized = normalizedTime;
                        // Force the state to not cycle, as the time has been set by the feature flag
                        settings.IsUIControlled = true;
                        settings.IsDayCycleEnabled = false;
                    }
                    else
                    {
                        float globalTime = settings.GlobalTimeOfDayNormalized;
                        settings.TimeOfDayNormalized = globalTime;
                        settings.TargetTimeOfDayNormalized = globalTime;
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
            debugLookEntries = entries;
            debugLookPresets = new ProvidedAsset<SkyboxLookPreset>?[entries.Length];

            var names = new List<string>(entries.Length + 1) { shippedPreset.name };

            for (var i = 0; i < entries.Length; i++)
                names.Add(entries[i].Name);

            var binding = new IndexedElementBinding(names, shippedPreset.name, evt =>
            {
                // A newer pick cancels the previous load so a late one cannot apply over it.
                debugLookCancellation = debugLookCancellation.SafeRestart();
                SwitchLookAsync(controller, shippedPreset, entries, evt.index, debugLookCancellation.Token).Forget();
            });

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
                    ProvidedAsset<SkyboxLookPreset> loaded;

                    if (debugLookPresets[index - 1] is { } cached)
                        loaded = cached;
                    else
                    {
                        AssetReferenceT<SkyboxLookPreset> reference = entries[index - 1].Preset;

                        // A load cancelled by an earlier pick keeps running on the reference, and the provisioner hands that
                        // handle back unfinished; waiting on it first means the provided asset is always complete.
                        if (reference.OperationHandle.IsValid())
                            await reference.OperationHandle.WithCancellation(ct);

                        loaded = await assetsProvisioner.ProvideMainAssetAsync(reference, ct);
                        debugLookPresets[index - 1] = loaded;
                    }

                    preset = loaded.Value;
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
            public SkyboxSettingsAsset Settings { get; private set; } = null!;
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
