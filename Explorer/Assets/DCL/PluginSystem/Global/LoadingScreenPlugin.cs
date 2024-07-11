using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.SceneLoadingScreens;
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

        public LoadingScreenPlugin(
            IAssetsProvisioner assetsProvisioner,
            IMVCManager mvcManager)
        {
            this.assetsProvisioner = assetsProvisioner;
            this.mvcManager = mvcManager;
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
                TimeSpan.FromSeconds(settings.MinimumScreenDisplayDuration)));
        }
    }
}
