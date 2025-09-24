using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Audio;
using DCL.DebugUtilities;
using DCL.DebugUtilities.UIBindings;
using DCL.FeatureFlags;
using DCL.Input;
using DCL.RealmNavigation;
using DCL.SceneLoadingScreens;
using DCL.Utilities;
using MVC;
using System;
using System.Threading;
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
        private readonly FeatureFlagsConfiguration featureFlagsConfiguration;

        private readonly ElementBinding<string> currentStageBinding = new (string.Empty);
        private readonly ElementBinding<string> assetStateBinding = new (string.Empty);


        public LoadingScreenPlugin(
            IAssetsProvisioner assetsProvisioner,
            IMVCManager mvcManager,
            AudioMixerVolumesController audioMixerVolumesController,
            IInputBlock inputBlock,
            IDebugContainerBuilder debugContainerBuilder,
            ILoadingStatus loadingStatus,
            FeatureFlagsConfiguration featureFlagsConfiguration)
        {
            this.assetsProvisioner = assetsProvisioner;
            this.mvcManager = mvcManager;
            this.audioMixerVolumesController = audioMixerVolumesController;
            this.inputBlock = inputBlock;
            this.debugContainerBuilder = debugContainerBuilder;
            this.loadingStatus = loadingStatus;
            this.featureFlagsConfiguration = featureFlagsConfiguration;
        }

        public void Dispose() { }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments) { }

        public async UniTask InitializeAsync(LoadingScreenPluginSettings settings, CancellationToken ct)
        {
            SceneLoadingScreenView prefab = (await assetsProvisioner.ProvideMainAssetAsync(settings.LoadingScreenPrefab, ct: ct)).Value;

            ControllerBase<SceneLoadingScreenView, SceneLoadingScreenController.Params>.ViewFactoryMethod? authScreenFactory =
                SceneLoadingScreenController.CreateLazily(prefab, null);

            var unityLocalizationSceneTipsProvider = new UnityLocalizationSceneTipsProvider(LocalizationSettings.StringDatabase, LocalizationSettings.AssetDatabase,
                settings.FallbackTipsTable, settings.FallbackImagesTable, TimeSpan.FromSeconds(settings.TipDisplayDuration));

            var tipsProvider = new TipsFromFeatureFlagDecorator(unityLocalizationSceneTipsProvider, featureFlagsConfiguration);

            await unityLocalizationSceneTipsProvider.InitializeAsync(ct);

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
