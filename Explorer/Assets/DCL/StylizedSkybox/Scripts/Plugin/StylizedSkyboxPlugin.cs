using Arch.Core;
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.DebugUtilities;
using DCL.DebugUtilities.UIBindings;
using DCL.FeatureFlags;
using DCL.PluginSystem;
using DCL.PluginSystem.Global;
using DCL.SceneRestrictionBusController.SceneRestrictionBus;
using ECS.SceneLifeCycle;
using System;
using System.Threading;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DCL.StylizedSkybox.Scripts.Plugin
{
    public class StylizedSkyboxPlugin : IDCLGlobalPlugin<StylizedSkyboxPlugin.StylizedSkyboxSettings>
    {
        private SkyboxController? skyboxController;
        private StylizedSkyboxSettingsAsset? skyboxSettings;
        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly Light directionalLight;
        private readonly FeatureFlagsCache featureFlagsCache;
        private readonly IScenesCache scenesCache;
        private readonly ISceneRestrictionBusController sceneRestrictionBusController;

        private readonly IDebugContainerBuilder debugContainerBuilder;
        private readonly ElementBinding<float> debugTimeOfDay = new(0);
        private readonly ElementBinding<string> debugTimeSource = new (nameof(SkyboxTimeSource.GLOBAL));

        public StylizedSkyboxPlugin(IAssetsProvisioner assetsProvisioner,
            Light directionalLight,
            IDebugContainerBuilder debugContainerBuilder,
            FeatureFlagsCache featureFlagsCache,
            IScenesCache scenesCache
           , ISceneRestrictionBusController sceneRestrictionBusController
            )
        {
            this.assetsProvisioner = assetsProvisioner;
            this.directionalLight = directionalLight;
            this.debugContainerBuilder = debugContainerBuilder;
            this.featureFlagsCache = featureFlagsCache;
            this.scenesCache = scenesCache;
            this.sceneRestrictionBusController = sceneRestrictionBusController;
        }

        public void Dispose()
        {
            if (skyboxSettings)
                skyboxSettings.SkyboxTimeSourceChanged -= OnSkyboxTimeSourceChanged;
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<World> builder, in GlobalPluginArguments arguments) { }

        public async UniTask InitializeAsync(StylizedSkyboxSettings settings, CancellationToken ct)
        {
            skyboxSettings = settings.SettingsAsset;
            skyboxSettings.Reset();

            skyboxController = Object.Instantiate((await assetsProvisioner.ProvideMainAssetAsync(skyboxSettings.StylizedSkyboxPrefab, ct: ct)).Value.GetComponent<SkyboxController>());
            AnimationClip skyboxAnimation = (await assetsProvisioner.ProvideMainAssetAsync(skyboxSettings.SkyboxAnimationCycle, ct: ct)).Value;

            skyboxController.Initialize(skyboxSettings.SkyboxMaterial,
                directionalLight,
                skyboxAnimation,
                featureFlagsCache,
                skyboxSettings,
                scenesCache
               , sceneRestrictionBusController
                );

            SetupDebugPanel();
        }

        private void SetupDebugPanel()
        {
            if(!skyboxController || !skyboxSettings) return;

            skyboxSettings.SkyboxTimeSourceChanged += OnSkyboxTimeSourceChanged;

            debugContainerBuilder.TryAddWidget("Skybox")
                                ?.AddSingleButton("Play", ()=> skyboxController.ForceSetDayCycleEnabled(true, SkyboxTimeSource.GLOBAL))
                                 .AddSingleButton("Pause", () =>
                                  {
                                      skyboxController.ForceSetDayCycleEnabled(false, SkyboxTimeSource.PLAYER_FIXED);
                                  })
                                 .AddFloatSliderField("Time", debugTimeOfDay, 0, 1)
                                 .AddSingleButton("SetTime", () =>
                                  {
                                      skyboxController.ForceSetTimeOfDay(debugTimeOfDay.Value, SkyboxTimeSource.PLAYER_FIXED);
                                  }) //TODO: replace this by a system to update the value
                                 .AddCustomMarker("TimeSource", debugTimeSource);
        }

        private void OnSkyboxTimeSourceChanged(SkyboxTimeSource source) =>
            debugTimeSource.Value = source.ToString();

        [Serializable]
        public class StylizedSkyboxSettings : IDCLPluginSettings
        {
            public StylizedSkyboxSettingsAsset SettingsAsset;
        }
    }
}
