using Arch.Core;
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Diagnostics;
using DCL.PluginSystem;
using DCL.PluginSystem.Global;
using DCL.SceneRestrictionBusController.SceneRestrictionBus;
using ECS.SceneLifeCycle;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;
using Object = UnityEngine.Object;

namespace DCL.SkyBox
{
    public class SkyboxPlugin : IDCLGlobalPlugin<SkyboxPlugin.SkyboxTimeSettings>
    {
        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly Light directionalLight;
        private readonly IScenesCache scenesCache;
        private readonly ISceneRestrictionBusController sceneRestrictionController;

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
            SkyboxTimeUpdateSystem.InjectToWorld(ref builder, skyboxSettings, scenesCache, sceneRestrictionController, skyboxRenderController);
        }

        public async UniTask InitializeAsync(SkyboxTimeSettings pluginSettings, CancellationToken ct)
        {
            try
            {
                skyboxSettings = (await assetsProvisioner.ProvideMainAssetAsync(pluginSettings.SettingsRef, ct)).Value;
                skyboxSettings.Reset();
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
        }

        public class SkyboxTimeSettings : IDCLPluginSettings
        {
            [field: SerializeField]
            public SkyboxSettingsAssetRef SettingsRef { get; private set; }
        }

        [Serializable]
        public class SkyboxSettingsAssetRef : AssetReferenceT<SkyboxSettingsAsset>
        {
            public SkyboxSettingsAssetRef(string guid) : base(guid)
            {
            }
        }
    }
}
