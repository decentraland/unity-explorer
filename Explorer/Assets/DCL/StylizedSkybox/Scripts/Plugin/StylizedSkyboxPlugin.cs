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
using DCL.Utilities;
using ECS;
using ECS.SceneLifeCycle;
using SceneRunner.Scene;
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
        private readonly FeatureFlagsCache featureFlagsCache;
        private readonly IScenesCache scenesCache;
        private StylizedSkyboxSettingsAsset? settingsAsset;
        private IRealmData? realmData;

        private IDisposable onRealmTypeUpdateSubscription;
        
        public StylizedSkyboxPlugin(
            IAssetsProvisioner assetsProvisioner,
            Light directionalLight,
            IDebugContainerBuilder debugContainerBuilder,
            FeatureFlagsCache featureFlagsCache,
            IScenesCache scenesCache,
            IRealmData? realmData)
        {
            timeOfDay = new ElementBinding<float>(0);
            this.assetsProvisioner = assetsProvisioner;
            this.directionalLight = directionalLight;
            this.debugContainerBuilder = debugContainerBuilder;
            this.featureFlagsCache = featureFlagsCache;
            this.scenesCache = scenesCache;
            this.realmData = realmData;
        }
        
        public void InjectToWorld(ref ArchSystemsWorldBuilder<World> builder, in GlobalPluginArguments arguments) { }
        
        public async UniTask InitializeAsync(StylizedSkyboxSettings settings, CancellationToken ct)
        {
            settingsAsset = settings.SettingsAsset;
            
            skyboxController = Object.Instantiate((await assetsProvisioner
                .ProvideMainAssetAsync(settingsAsset.StylizedSkyboxPrefab, ct: ct))
                .Value
                .GetComponent<SkyboxController>());
            
            AnimationClip skyboxAnimation = (await assetsProvisioner
                .ProvideMainAssetAsync(settingsAsset.SkyboxAnimationCycle, ct: ct))
                .Value;

            skyboxController.Initialize(
                settingsAsset.SkyboxMaterial,
                directionalLight,
                skyboxAnimation,
                featureFlagsCache,
                settingsAsset);

            if (realmData != null)
                onRealmTypeUpdateSubscription = realmData.RealmType.Subscribe(OnRealmUpdate);
            
            
            debugContainerBuilder.TryAddWidget("Skybox")
                                ?.AddSingleButton("Play", () => skyboxController.UseDynamicTime = true)
                                 .AddSingleButton("Pause", () => skyboxController.UseDynamicTime = false)
                                 .AddFloatSliderField("Time", timeOfDay, 0, 1)
                                 .AddSingleButton("SetTime", () => skyboxController.SetTimeOverride(timeOfDay.Value)); //TODO: replace this by a system to update the value
        }

        private void OnRealmUpdate(RealmKind kind)
        {
            Debug.Log("Realm updated to " + kind);
            // NOTE: only use fixedTime if the realm is world
            // NOTE or a local scene it can be world and local scene at the same time?
            if (realmData?.RealmType.Value is 
                RealmKind.World or
                RealmKind.LocalScene)
                
                scenesCache.OnCurrentSceneChanged += HandleSceneChanged;
        }
        
        private void HandleSceneChanged(ISceneFacade? scene)
        {
            if (scene == null) return;
            
            var meta = scene.SceneData.SceneEntityDefinition.metadata;
            if (meta.IsTimeFixed)
            {
                float secs = meta.worldConfiguration!.SkyboxConfig!.FixedTime!.Value;
                if (settingsAsset != null)
                {
                    settingsAsset.FixedTime = secs;
                    settingsAsset.UseDynamicTime = false;
                }
            }
        }
        
        public void Dispose()
        {
            scenesCache.OnCurrentSceneChanged -= HandleSceneChanged;
            onRealmTypeUpdateSubscription?.Dispose();
        }

        [Serializable]
        public class StylizedSkyboxSettings : IDCLPluginSettings
        {
            public StylizedSkyboxSettingsAsset SettingsAsset;
        }
    }
}
