using Arch.Core;
using CRDT;
using CrdtEcsBridge.Components;
using Cysharp.Threading.Tasks;
using DCL.ApplicationVersionGuard;
using DCL.Audio;
using DCL.AuthenticationScreenFlow;
using DCL.DebugUtilities;
using DCL.Diagnostics;
using DCL.Input.Component;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Platforms;
using DCL.PluginSystem;
using DCL.PluginSystem.Global;
using DCL.SceneLoadingScreens.SplashScreen;
using DCL.Utilities;
using DCL.Utilities.Extensions;
using Global.AppArgs;
using MVC;
using Plugins.TexturesFuse.TexturesServerWrap.CompressShaders;
using Plugins.TexturesFuse.TexturesServerWrap.Unzips;
using SceneRunner.Debugging;
using System;
using System.Linq;
using System.Threading;
using UnityEngine;
using UnityEngine.UIElements;

namespace Global.Dynamic
{
    public class MainSceneLoader : MonoBehaviour
    {
        [Header("Startup Config")] [SerializeField]
        private RealmLaunchSettings launchSettings = null!;

        [Space]
        [SerializeField] private DebugViewsCatalog debugViewsCatalog = new ();

        [Space]
        [SerializeField] private DebugSettings.DebugSettings debugSettings = new ();

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
        [SerializeField] private Animator logoAnimation = null!;
        [SerializeField] private AudioClipConfig backgroundMusic = null!;
        [SerializeField] private WorldInfoTool worldInfoTool = null!;

        private BootstrapContainer? bootstrapContainer;
        private StaticContainer? staticContainer;
        private DynamicWorldContainer? dynamicWorldContainer;
        private GlobalWorld? globalWorld;

        private void Awake()
        {
            InitializeFlowAsync(destroyCancellationToken).Forget();
        }

        private void OnDestroy()
        {
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

            bootstrapContainer?.Dispose();

            ReportHub.Log(ReportCategory.ENGINE, "OnDestroy successfully finished");
        }

        private async UniTask InitializeFlowAsync(CancellationToken ct)
        {
            var applicationParametersParser = new ApplicationParametersParser(
#if UNITY_EDITOR
                debugSettings.AppParameters
#else
                Environment.GetCommandLineArgs()
#endif
            );

            ITexturesFuse texturesFuse = ITexturesFuse.NewDefault();
            ICompressShaders compressShaders = new CompressShaders(texturesFuse, IPlatform.DEFAULT);

            if (applicationParametersParser.HasFlag(ICompressShaders.CMD_ARGS))
            {
                await compressShaders.WarmUpIfRequiredAsync(ct);
                IPlatform.DEFAULT.Quit();
                return;
            }

            ISystemMemoryCap memoryCap = new SystemMemoryCap(MemoryCapMode.MAX_SYSTEM_MEMORY); // we use max memory on the loading screen

            settings.ApplyConfig(applicationParametersParser);
            launchSettings.ApplyConfig(applicationParametersParser);

            World world = World.Create();

            bootstrapContainer = await BootstrapContainer.CreateAsync(debugSettings, sceneLoaderSettings: settings,
                globalPluginSettingsContainer, launchSettings,
                applicationParametersParser,
                world,
                destroyCancellationToken);

            IBootstrap bootstrap = bootstrapContainer!.Bootstrap!;

            try
            {
                var splashScreen = new SplashScreen(splashScreenAnimation, splashRoot, debugSettings.ShowSplash);
                bootstrap.PreInitializeSetup(cursorRoot, debugUiRoot, splashScreen, destroyCancellationToken);

                bool isLoaded;
                Entity playerEntity = world.Create(new CRDTEntity(SpecialEntitiesID.PLAYER_ENTITY));
                (staticContainer, isLoaded) = await bootstrap.LoadStaticContainerAsync(bootstrapContainer, globalPluginSettingsContainer, debugViewsCatalog, playerEntity, texturesFuse, memoryCap, ct);

                if (!isLoaded)
                {
                    GameReports.PrintIsDead();
                    return;
                }

                bootstrap.InitializePlayerEntity(staticContainer!, playerEntity);

                (dynamicWorldContainer, isLoaded) = await bootstrap.LoadDynamicWorldContainerAsync(bootstrapContainer, staticContainer!, scenePluginSettingsContainer, settings,
                    dynamicSettings, uiToolkitRoot, cursorRoot, splashScreen, backgroundMusic, worldInfoTool.EnsureNotNull(), playerEntity,
                    applicationParametersParser,
                    destroyCancellationToken);

                if (!isLoaded)
                {
                    GameReports.PrintIsDead();
                    return;
                }

                await bootstrap.InitializeFeatureFlagsAsync(bootstrapContainer.IdentityCache!.Identity, bootstrapContainer.DecentralandUrlsSource, staticContainer!, ct);

                if (await DoesApplicationRequireVersionUpdateAsync(applicationParametersParser, splashScreen, ct))
                    return; // stop bootstrapping;

                DisableInputs();

                if (await bootstrap.InitializePluginsAsync(staticContainer!, dynamicWorldContainer!, scenePluginSettingsContainer, globalPluginSettingsContainer, ct))
                {
                    GameReports.PrintIsDead();
                    return;
                }

                globalWorld = bootstrap.CreateGlobalWorld(bootstrapContainer, staticContainer!, dynamicWorldContainer!, debugUiRoot, playerEntity);

                await bootstrap.LoadStartingRealmAsync(dynamicWorldContainer!, ct);

                await bootstrap.UserInitializationAsync(dynamicWorldContainer!, globalWorld, playerEntity, splashScreen, ct);

                //This is done in order to release the memory usage of the splash screen logo animation sprites
                //The logo is used only at first launch, so we can safely release it after the game is loaded
                logoAnimation.StopPlayback();
                logoAnimation.runtimeAnimatorController = null;

                memoryCap.Mode = MemoryCapMode.FROM_SETTINGS;
                RestoreInputs();
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

        private async UniTask<bool> DoesApplicationRequireVersionUpdateAsync(ApplicationParametersParser applicationParametersParser, SplashScreen splashScreen, CancellationToken ct)
        {
            applicationParametersParser.TryGetValue(ApplicationVersionGuard.SIMULATE_VERSION_CLI_ARG, out string? version);
            string? currentVersion = version ?? Application.version;

            bool runVersionControl = debugSettings.EnableVersionUpdateGuard;

            if (applicationParametersParser.HasDebugFlag() && !Application.isEditor)
                runVersionControl = applicationParametersParser.TryGetValue(ApplicationVersionGuard.ENABLE_VERSION_CONTROL_CLI_ARG, out string? enforceDebugMode) && enforceDebugMode == "true";

            if (!runVersionControl)
                return false;

            var appVersionGuard = new ApplicationVersionGuard(staticContainer!.WebRequestsContainer.WebRequestController, bootstrapContainer!.WebBrowser);
            string? latestVersion = await appVersionGuard.GetLatestVersionAsync(ct);

            if (!currentVersion.IsOlderThan(latestVersion))
                return false;

            splashScreen.Hide();

            var appVerRedirectionScreenPrefab = await bootstrapContainer!.AssetsProvisioner!.ProvideMainAssetAsync(dynamicSettings.AppVerRedirectionScreenPrefab, ct);

            ControllerBase<LauncherRedirectionScreenView, ControllerNoData>.ViewFactoryMethod authScreenFactory =
                LauncherRedirectionScreenController.CreateLazily(appVerRedirectionScreenPrefab.Value.GetComponent<LauncherRedirectionScreenView>(), null);

            var launcherRedirectionScreenController = new LauncherRedirectionScreenController(appVersionGuard, authScreenFactory, currentVersion, latestVersion);
            dynamicWorldContainer!.MvcManager.RegisterController(launcherRedirectionScreenController);

            await dynamicWorldContainer!.MvcManager.ShowAsync(LauncherRedirectionScreenController.IssueCommand(), ct);
            return true;
        }

        private void DisableInputs()
        {
            // We disable Inputs directly because otherwise before login (so before the Input component was created and the system that handles it is working)
            // all inputs will be valid, and it allows for weird behaviour, including opening menus that are not ready to be open yet.
            staticContainer!.InputProxy.StrictObject.Shortcuts.Disable();
            staticContainer.InputProxy.StrictObject.Player.Disable();
            staticContainer.InputProxy.StrictObject.Emotes.Disable();
            staticContainer.InputProxy.StrictObject.EmoteWheel.Disable();
            staticContainer.InputProxy.StrictObject.FreeCamera.Disable();
            staticContainer.InputProxy.StrictObject.Camera.Disable();
        }

        private void RestoreInputs()
        {
            // We enable Inputs through the inputBlock so the block counters can be properly updated and the component Active flags are up-to-date as well
            // We restore all inputs except EmoteWheel and FreeCamera as they should be disabled by default
            staticContainer!.InputBlock.Enable(InputMapComponent.Kind.SHORTCUTS, InputMapComponent.Kind.PLAYER, InputMapComponent.Kind.EMOTES, InputMapComponent.Kind.CAMERA);
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
