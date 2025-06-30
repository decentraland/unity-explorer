using Arch.Core;
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.DebugUtilities;
using DCL.Diagnostics;
using DCL.PluginSystem;
using DCL.PluginSystem.Global;
using DCL.SceneRestrictionBusController.SceneRestrictionBus;
using ECS.SceneLifeCycle;
using System;
using System.Threading;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DCL.SkyBox
{
    public class SkyboxPlugin : IDCLGlobalPlugin<SkyboxPlugin.SkyboxSettings>
    {
        private SkyboxSettingsAsset skyboxSettings;
        private SkyboxRenderController skyboxRenderController;

        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly Light directionalLight;
        private readonly IScenesCache scenesCache;
        private readonly ISceneRestrictionBusController sceneRestrictionController;
        private readonly IDebugContainerBuilder debugContainerBuilder;

        public SkyboxPlugin(IAssetsProvisioner assetsProvisioner,
            Light directionalLight,
            IScenesCache scenesCache,
            ISceneRestrictionBusController sceneRestrictionController,
            IDebugContainerBuilder debugContainerBuilder)
        {
            this.assetsProvisioner = assetsProvisioner;
            this.directionalLight = directionalLight;
            this.scenesCache = scenesCache;
            this.sceneRestrictionController = sceneRestrictionController;
            this.debugContainerBuilder = debugContainerBuilder;
        }

        public void Dispose() { }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<World> builder, in GlobalPluginArguments arguments)
        {
            SkyboxRenderUpdateSystem.InjectToWorld(ref builder, skyboxRenderController, skyboxSettings);
            SkyboxTimeUpdateSystem.InjectToWorld(ref builder, skyboxSettings, scenesCache, sceneRestrictionController);
        }

        public async UniTask InitializeAsync(SkyboxSettings settings, CancellationToken ct)
        {
            try
            {
                skyboxSettings = settings.SettingsAsset;
                skyboxSettings.Reset();
                skyboxRenderController = Object.Instantiate((await assetsProvisioner.ProvideMainAssetAsync(skyboxSettings.SkyboxRenderControllerPrefab, ct: ct)).Value);

                AnimationClip skyboxAnimation = (await assetsProvisioner.ProvideMainAssetAsync(skyboxSettings.SkyboxAnimationCycle, ct: ct)).Value;

                skyboxRenderController.Initialize(
                    skyboxSettings.SkyboxMaterial,
                    directionalLight,
                    skyboxAnimation,
                    skyboxSettings.initialTimeOfDay
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
        }

        [Serializable]
        public class SkyboxSettings : IDCLPluginSettings
        {
            public SkyboxSettingsAsset SettingsAsset;
        }
    }
}
