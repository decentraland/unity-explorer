using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Audio;
using DCL.Input;
using DCL.SceneLoadingScreens;
using MVC;
using System;
using System.Threading;
using DCL.DebugUtilities;
using DCL.DebugUtilities.UIBindings;
using DCL.UserInAppInitializationFlow;
using DCL.Utilities;
using UnityEngine.Localization.Settings;

namespace DCL.PluginSystem.Global
{
    public partial class LoadingScreenPlugin : IDCLGlobalPlugin<LoadingScreenPlugin.LoadingScreenPluginSettings>
    {
        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly IMVCManager mvcManager;
        private readonly AudioMixerVolumesController audioMixerVolumesController;
        private readonly IInputBlock inputBlock;
        private readonly IDebugContainerBuilder debugContainerBuilder;
        private readonly ILoadingStatus loadingStatus;

        
        private ElementBinding<string> currentStageBinding = new ElementBinding<string>(string.Empty);
        private ElementBinding<string> assetStateBinding = new ElementBinding<string>(string.Empty);


        public LoadingScreenPlugin(
            IAssetsProvisioner assetsProvisioner,
            IMVCManager mvcManager,
            AudioMixerVolumesController audioMixerVolumesController,
            IInputBlock inputBlock,
            IDebugContainerBuilder debugContainerBuilder,
            ILoadingStatus loadingStatus)
        {
            this.assetsProvisioner = assetsProvisioner;
            this.mvcManager = mvcManager;
            this.audioMixerVolumesController = audioMixerVolumesController;
            this.inputBlock = inputBlock;
            this.debugContainerBuilder = debugContainerBuilder;
            this.loadingStatus = loadingStatus;
        }

        public void Dispose() { }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments) { }

        public async UniTask InitializeAsync(LoadingScreenPluginSettings settings, CancellationToken ct)
        {
            SceneLoadingScreenView prefab = (await assetsProvisioner.ProvideMainAssetAsync(settings.LoadingScreenPrefab, ct: ct)).Value;
            SceneTipsConfigurationSO fallbackTipsConfig = (await assetsProvisioner.ProvideMainAssetAsync(settings.FallbackTipsConfiguration, ct: ct)).Value;

            ControllerBase<SceneLoadingScreenView, SceneLoadingScreenController.Params>.ViewFactoryMethod? authScreenFactory =
                SceneLoadingScreenController.CreateLazily(prefab, null);

            var tipsProvider = new UnityLocalizationSceneTipsProvider(LocalizationSettings.StringDatabase, LocalizationSettings.AssetDatabase,
                fallbackTipsConfig, settings.FallbackTipsTable, settings.FallbackImagesTable,
                TimeSpan.FromSeconds(settings.TipDisplayDuration));

            await tipsProvider.InitializeAsync(ct);
            
            mvcManager.RegisterController(new SceneLoadingScreenController(authScreenFactory, tipsProvider,
                TimeSpan.FromSeconds(settings.MinimumScreenDisplayDuration), audioMixerVolumesController, inputBlock));

            loadingStatus.CurrentStage.Subscribe(stage => currentStageBinding.Value = stage.ToString());
            loadingStatus.AssetState.Subscribe(assetState => assetStateBinding.Value = assetState);
            
            currentStageBinding.Value= loadingStatus.CurrentStage.Value.ToString();
            assetStateBinding.Value = loadingStatus.AssetState.Value;
            
            debugContainerBuilder
                .TryAddWidget("Loading Screen")?
                .AddCustomMarker("Current Stage", currentStageBinding)
                .AddCustomMarker("Assets to load", assetStateBinding);
        }
    }
}
