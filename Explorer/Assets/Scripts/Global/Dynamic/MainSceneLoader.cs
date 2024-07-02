using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.AssetsProvision.Provisions;
using DCL.Audio;
using DCL.DebugUtilities;
using DCL.Diagnostics;
using DCL.PluginSystem;
using DCL.PluginSystem.Global;
using DCL.Utilities;
using System;
using System.Linq;
using System.Threading;
using UnityEngine;
using UnityEngine.UIElements;

namespace Global.Dynamic
{
    public class MainSceneLoader : MonoBehaviour
    {
        [Header("Startup Config")]
        [SerializeField] private RealmLaunchSettings launchSettings;

        [Space]
        [SerializeField] private DebugViewsCatalog debugViewsCatalog = new ();

        [Space]
        [SerializeField] private bool showSplash;
        [SerializeField] private bool showAuthentication;
        [SerializeField] private bool showLoading;
        [SerializeField] private bool enableLandscape;
        [SerializeField] private bool enableLOD;
        [SerializeField] private bool analyticsEnabled;

        [Header("References")]
        [SerializeField] private PluginSettingsContainer globalPluginSettingsContainer = null!;
        [SerializeField] private PluginSettingsContainer scenePluginSettingsContainer = null!;
        [SerializeField] private UIDocument uiToolkitRoot = null!;
        [SerializeField] private UIDocument cursorRoot = null!;
        [SerializeField] private UIDocument debugUiRoot = null!;
        [SerializeField] private DynamicSceneLoaderSettings settings = null!;
        [SerializeField] private DynamicSettings dynamicSettings = null!;
        [SerializeField] private GameObject splashRoot = null!;
        [SerializeField] private Animator splashScreenAnimation = null!;
        [SerializeField] private AudioClipConfig backgroundMusic = null!;

        private DynamicWorldContainer? dynamicWorldContainer;
        private GlobalWorld? globalWorld;
        private StaticContainer? staticContainer;

        private IBootstrap? bootstrap;

        private void Awake()
        {
            ErrorTraceAssetsProvisioner assetsProvisioner = new AddressablesProvisioner().WithErrorTrace();
            var coreBootstrap = new Bootstrap(showSplash, showAuthentication, showLoading, enableLOD, enableLandscape);

            bootstrap = !Debug.isDebugBuild || analyticsEnabled
                ? new BootstrapAnalyticsDecorator(coreBootstrap, assetsProvisioner, globalPluginSettingsContainer.GetSettings<AnalyticsSettings>())
                : coreBootstrap;

            InitializeFlowAsync(assetsProvisioner, destroyCancellationToken).Forget();
        }

        private void OnDestroy()
        {
            bootstrap?.Dispose();

            if (dynamicWorldContainer != null)
            {
                foreach (IDCLGlobalPlugin plugin in dynamicWorldContainer.GlobalPlugins)
                    plugin.SafeDispose(ReportCategory.ENGINE);

                if (globalWorld != null)
                    dynamicWorldContainer.RealmController.DisposeGlobalWorld();

                dynamicWorldContainer.SafeDispose(ReportCategory.ENGINE);
            }

            if (staticContainer != null)
            {
                // Exclude SharedPlugins as they were disposed as they were already disposed of as `GlobalPlugins`
                foreach (IDCLPlugin worldPlugin in staticContainer.ECSWorldPlugins.Except<IDCLPlugin>(staticContainer.SharedPlugins))
                    worldPlugin.SafeDispose(ReportCategory.ENGINE);

                staticContainer.SafeDispose(ReportCategory.ENGINE);
            }

            ReportHub.Log(ReportCategory.ENGINE, "OnDestroy successfully finished");
        }

        private async UniTask InitializeFlowAsync(ErrorTraceAssetsProvisioner assetsProvisioner, CancellationToken ct)
        {
            try
            {
                bool isLoaded;

                await bootstrap!.PreInitializeSetup(launchSettings, cursorRoot, debugUiRoot, splashRoot, debugViewsCatalog, destroyCancellationToken);

                (staticContainer, isLoaded) = await bootstrap.LoadStaticContainerAsync(globalPluginSettingsContainer, settings, assetsProvisioner, ct);

                if (!isLoaded)
                {
                    GameReports.PrintIsDead();
                    return;
                }

                (dynamicWorldContainer, isLoaded) = await bootstrap.LoadDynamicWorldContainerAsync(staticContainer!, scenePluginSettingsContainer, settings,
                    dynamicSettings, launchSettings, uiToolkitRoot, cursorRoot, splashScreenAnimation, backgroundMusic, destroyCancellationToken);

                if (!isLoaded)
                {
                    GameReports.PrintIsDead();
                    return;
                }

                await bootstrap.InitializeFeatureFlagsAsync(staticContainer!, ct);

                if (await bootstrap.InitializePluginsAsync(staticContainer!, dynamicWorldContainer!, scenePluginSettingsContainer, globalPluginSettingsContainer, ct))
                {
                    GameReports.PrintIsDead();
                    return;
                }

                Entity playerEntity;
                (globalWorld, playerEntity) = bootstrap.CreateGlobalWorldAndPlayer(staticContainer!, dynamicWorldContainer!, debugUiRoot);
                await bootstrap.LoadStartingRealmAsync(dynamicWorldContainer!, ct);
                await bootstrap.UserInitializationAsync(dynamicWorldContainer!, globalWorld, playerEntity, splashScreenAnimation, splashRoot, ct);
            }
            catch (OperationCanceledException)
            {
                // ignore
            }
            catch (Exception)
            {
                // unhandled exception
                GameReports.PrintIsDead();
                throw;
            }
        }

        [ContextMenu(nameof(ValidateSettingsAsync))]
        public async UniTask ValidateSettingsAsync()
        {
            using var scope = new CheckingScope(ReportData.UNSPECIFIED);

            await UniTask.WhenAll(
                globalPluginSettingsContainer.EnsureValidAsync(),
                scenePluginSettingsContainer.EnsureValidAsync()
            );

            ReportHub.Log(ReportData.UNSPECIFIED, "Success checking");
        }

        private readonly struct CheckingScope : IDisposable
        {
            private readonly ReportData data;

            public CheckingScope(ReportData data)
            {
                this.data = data;
                ReportHub.Log(data, "Start checking");
            }

            public void Dispose()
            {
                ReportHub.Log(data, "Finish checking");
            }
        }
    }
}
