using Arch.Core;
using CommunicationData.URLHelpers;
using CRDT;
using CrdtEcsBridge.Components;
using Cysharp.Threading.Tasks;
using DCL.ApplicationBlocklistGuard;
using DCL.ApplicationMinimumSpecsGuard;
using DCL.ApplicationVersionGuard;
using DCL.Audio;
using DCL.AuthenticationScreenFlow;
using DCL.Browser;
using DCL.Browser.DecentralandUrls;
using DCL.DebugUtilities;
using DCL.Diagnostics;
using DCL.FeatureFlags;
using DCL.Infrastructure.Global;
using DCL.Input.Component;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Optimization.PerformanceBudgeting;
using DCL.PluginSystem;
using DCL.PluginSystem.Global;
using DCL.Prefs;
using DCL.SceneLoadingScreens.SplashScreen;
using DCL.Utilities;
using DCL.Utilities.Extensions;
using DCL.Web3.Accounts.Factory;
using DCL.Web3.Identities;
using DCL.WebRequests;
using DCL.WebRequests.Analytics;
using ECS.StreamableLoading.Cache.Disk;
using ECS.StreamableLoading.Cache.Disk.CleanUp;
using ECS.StreamableLoading.Cache.Disk.Lock;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Common.Components;
using Global.AppArgs;
using Global.Dynamic.LaunchModes;
using Global.Dynamic.RealmUrl;
using Global.Dynamic.RealmUrl.Names;
using Global.Versioning;
using MVC;
using SceneRunner.Debugging;
using System;
using System.Linq;
using System.Threading;
using DCL.PerformanceAndDiagnostics.Analytics;
using TMPro;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.UIElements;
using Utility;
using MinimumSpecsScreenView = DCL.ApplicationMinimumSpecsGuard.MinimumSpecsScreenView;

namespace Global.Dynamic
{
    public class MainSceneLoader : MonoBehaviour, ICoroutineRunner
    {
        [Header("Startup Config")] [SerializeField]
        private RealmLaunchSettings launchSettings = null!;

        [Space]
        [SerializeField] private DecentralandEnvironment decentralandEnvironment;

        [Space]
        [SerializeField] private DebugViewsCatalog debugViewsCatalog = new ();

        [Space]
        [SerializeField] private DebugSettings.DebugSettings debugSettings = new ();

        [Header("References")]
        [SerializeField] private PluginSettingsContainer globalPluginSettingsContainer = null!;
        [SerializeField] private PluginSettingsContainer scenePluginSettingsContainer = null!;
        [SerializeField] private UIDocument uiToolkitRoot = null!;
        [SerializeField] private UIDocument scenesUIRoot = null!;
        [SerializeField] private UIDocument cursorRoot = null!;
        [SerializeField] private UIDocument debugUiRoot = null!;
        [SerializeField] private DynamicSceneLoaderSettings settings = null!;
        [SerializeField] private DynamicSettings dynamicSettings = null!;
        [SerializeField] private GameObject splashRoot = null!;
        [SerializeField] private TMP_Text splashScreenText = null!;
        [SerializeField] private Animator splashScreenAnimation = null!;
        [SerializeField] private Animator logoAnimation = null!;
        [SerializeField] private AudioClipConfig backgroundMusic = null!;
        [SerializeField] private WorldInfoTool worldInfoTool = null!;
        [SerializeField] private AssetReferenceGameObject untrustedRealmConfirmationPrefab = null!;

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

        public void ApplyConfig(IAppArgs applicationParametersParser)
        {
            if (applicationParametersParser.TryGetValue(AppArgsFlags.ENVIRONMENT, out string? environment))
                ParseEnvironment(environment!);
        }

        private void ParseEnvironment(string environment)
        {
            if (Enum.TryParse(environment, true, out DecentralandEnvironment env))
                decentralandEnvironment = env;
        }

        private async UniTask InitializeFlowAsync(CancellationToken ct)
        {
            IAppArgs applicationParametersParser = new ApplicationParametersParser(
#if UNITY_EDITOR
                debugSettings.AppParameters
#else
                Environment.GetCommandLineArgs()
#endif
            );

            DCLVersion dclVersion = DCLVersion.FromAppArgs(applicationParametersParser);
            DiagnosticInfoUtils.LogSystem(dclVersion.Version);

            const bool KTX_ENABLED = true;

            // Memory limit
            bool hasSimulatedMemory = applicationParametersParser.TryGetValue(AppArgsFlags.SIMULATE_MEMORY, out string simulatedMemory);
            int systemMemory = hasSimulatedMemory ? int.Parse(simulatedMemory) : SystemInfo.systemMemorySize;
            ISystemMemoryCap memoryCap = hasSimulatedMemory
                ? new SystemMemoryCap(systemMemory)
                : new SystemMemoryCap();

            ApplyConfig(applicationParametersParser);
            launchSettings.ApplyConfig(applicationParametersParser);

            World world = World.Create();

            var splashScreen = new SplashScreen(splashScreenAnimation, splashRoot, debugSettings.ShowSplash, splashScreenText);
            var decentralandUrlsSource = new DecentralandUrlsSource(decentralandEnvironment, launchSettings);
            DiagnosticInfoUtils.LogEnvironment(decentralandUrlsSource);

            var web3AccountFactory = new Web3AccountFactory();
            var identityCache = new IWeb3IdentityCache.Default(web3AccountFactory);
            var debugContainerBuilder = DebugUtilitiesContainer.Create(debugViewsCatalog, applicationParametersParser.HasDebugFlag()).Builder;
            var staticSettings = (globalPluginSettingsContainer as IPluginSettingsContainer).GetSettings<StaticSettings>();
            var webRequestsContainer = WebRequestsContainer.Create(identityCache, debugContainerBuilder, decentralandUrlsSource, staticSettings.CoreWebRequestsBudget, staticSettings.SceneWebRequestsBudget, KTX_ENABLED);
            var realmUrls = new RealmUrls(launchSettings, new RealmNamesMap(webRequestsContainer.WebRequestController), decentralandUrlsSource);

            var diskCache = NewInstanceDiskCache(applicationParametersParser, launchSettings);
            var partialsDiskCache = NewInstancePartialDiskCache(applicationParametersParser, launchSettings);

            bootstrapContainer = await BootstrapContainer.CreateAsync(
                debugSettings,
                sceneLoaderSettings: settings,
                decentralandUrlsSource,
                webRequestsContainer,
                identityCache,
                globalPluginSettingsContainer,
                launchSettings,
                applicationParametersParser,
                splashScreen,
                realmUrls,
                diskCache,
                partialsDiskCache,
                world,
                decentralandEnvironment,
                dclVersion,
                destroyCancellationToken
            );

            IBootstrap bootstrap = bootstrapContainer!.Bootstrap!;

            try
            {
                await bootstrap.PreInitializeSetupAsync(cursorRoot, debugUiRoot, destroyCancellationToken);

                bool isLoaded;
                Entity playerEntity = world.Create(new CRDTEntity(SpecialEntitiesID.PLAYER_ENTITY));
                bool hasDebugFlag = applicationParametersParser.HasDebugFlag();
                (staticContainer, isLoaded) = await bootstrap.LoadStaticContainerAsync(bootstrapContainer, globalPluginSettingsContainer, debugContainerBuilder, playerEntity, memoryCap, scenesUIRoot, hasDebugFlag, ct);

                if (!isLoaded)
                {
                    GameReports.PrintIsDead();
                    return;
                }

                bootstrap.InitializePlayerEntity(staticContainer!, playerEntity);

                await bootstrap.InitializeFeatureFlagsAsync(bootstrapContainer.IdentityCache!.Identity,
                    bootstrapContainer.DecentralandUrlsSource, staticContainer!, ct);

                bootstrap.ApplyFeatureFlagConfigs(FeatureFlagsConfiguration.Instance);
                staticContainer.SceneLoadingLimit.SetEnabled(FeatureFlagsConfiguration.Instance.IsEnabled(FeatureFlagsStrings.SCENE_MEMORY_LIMIT));

                (dynamicWorldContainer, isLoaded) = await bootstrap.LoadDynamicWorldContainerAsync(
                    bootstrapContainer,
                    staticContainer!,
                    scenePluginSettingsContainer,
                    settings,
                    dynamicSettings,
                    uiToolkitRoot,
                    scenesUIRoot,
                    cursorRoot,
                    backgroundMusic,
                    worldInfoTool.EnsureNotNull(),
                    playerEntity,
                    applicationParametersParser,
                    coroutineRunner: this,
                    dclVersion,
                    destroyCancellationToken);

                if (!isLoaded)
                {
                    GameReports.PrintIsDead();
                    return;
                }

                await RegisterBlockedPopupAsync(bootstrapContainer.WebBrowser, ct);

                await VerifyMinimumHardwareRequirementMetAsync(applicationParametersParser, bootstrapContainer.WebBrowser, bootstrapContainer.Analytics, ct);

                if (await DoesApplicationRequireVersionUpdateAsync(applicationParametersParser, splashScreen, ct))
                    return; // stop bootstrapping;

                if (!await IsTrustedRealmAsync(decentralandUrlsSource, ct))
                {
                    splashScreen.Hide();

                    if (!await ShowUntrustedRealmConfirmationAsync(ct))
                    {
#if UNITY_EDITOR
                        EditorApplication.isPlaying = false;
#else
                        Application.Quit();
#endif

                        return;
                    }

                    splashScreen.Show();
                }

                DisableInputs();

                if (await bootstrap.InitializePluginsAsync(staticContainer!, dynamicWorldContainer!, scenePluginSettingsContainer, globalPluginSettingsContainer, ct))
                {
                    GameReports.PrintIsDead();
                    return;
                }

                globalWorld = bootstrap.CreateGlobalWorld(bootstrapContainer, staticContainer!, dynamicWorldContainer!, debugUiRoot, playerEntity);

                await bootstrap.LoadStartingRealmAsync(dynamicWorldContainer!, ct);

                await bootstrap.UserInitializationAsync(dynamicWorldContainer!, globalWorld, playerEntity, ct);

                //This is done in order to release the memory usage of the splash screen logo animation sprites
                //The logo is used only at first launch, so we can safely release it after the game is loaded
                logoAnimation.StopPlayback();
                logoAnimation.runtimeAnimatorController = null;

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

        private async UniTask RegisterBlockedPopupAsync(IWebBrowser webBrowser, CancellationToken ct)
        {
            var blockedPopupPrefab = await bootstrapContainer!.AssetsProvisioner!.ProvideMainAssetAsync(dynamicSettings.BlockedScreenPrefab, ct);

            ControllerBase<BlockedScreenView, ControllerNoData>.ViewFactoryMethod viewFactory =
                BlockedScreenController.CreateLazily(blockedPopupPrefab.Value.GetComponent<BlockedScreenView>(), null);

            var launcherRedirectionScreenController = new BlockedScreenController(viewFactory, webBrowser);
            dynamicWorldContainer!.MvcManager.RegisterController(launcherRedirectionScreenController);
        }

        private async UniTask VerifyMinimumHardwareRequirementMetAsync(IAppArgs applicationParametersParser, IWebBrowser webBrowser, IAnalyticsController analytics, CancellationToken ct)
        {
            var minimumSpecsGuard = new MinimumSpecsGuard(new DefaultSpecProfileProvider());

            bool hasMinimumSpecs = minimumSpecsGuard.HasMinimumSpecs();
            bool userWantsToSkip = DCLPlayerPrefs.GetBool(DCLPrefKeys.DONT_SHOW_MIN_SPECS_SCREEN);
            bool forceShow = applicationParametersParser.HasFlag(AppArgsFlags.FORCE_MINIMUM_SPECS_SCREEN);

            bootstrapContainer.DiagnosticsContainer.AddSentryScopeConfigurator(scope =>
            {
                bootstrapContainer.DiagnosticsContainer.Sentry!.AddMeetMinimumRequirements(scope, hasMinimumSpecs);
            });


            bool shouldShowScreen = forceShow || (!userWantsToSkip && !hasMinimumSpecs);

            if (!shouldShowScreen)
                return;

            var minimumRequirementsPrefab = await bootstrapContainer!
                .AssetsProvisioner!
                .ProvideMainAssetAsync(dynamicSettings.MinimumSpecsScreenPrefab, ct);

            ControllerBase<MinimumSpecsScreenView, ControllerNoData>.ViewFactoryMethod viewFactory = MinimumSpecsScreenController
                .CreateLazily(minimumRequirementsPrefab.Value.GetComponent<MinimumSpecsScreenView>(), null);

            var minimumSpecsResults = minimumSpecsGuard.Results;
            var minimumSpecsScreenController = new MinimumSpecsScreenController(viewFactory, webBrowser, analytics, minimumSpecsResults);
            dynamicWorldContainer!.MvcManager.RegisterController(minimumSpecsScreenController);
            dynamicWorldContainer!.MvcManager.ShowAsync(MinimumSpecsScreenController.IssueCommand(), ct).Forget();
            await minimumSpecsScreenController.HoldingTask.Task;
        }

        private async UniTask<bool> DoesApplicationRequireVersionUpdateAsync(IAppArgs applicationParametersParser, SplashScreen splashScreen, CancellationToken ct)
        {
            DCLVersion currentVersion = DCLVersion.FromAppArgs(applicationParametersParser);
            bool runVersionControl = debugSettings.EnableVersionUpdateGuard;

            if (!Application.isEditor)
                runVersionControl = !applicationParametersParser.HasDebugFlag() && !applicationParametersParser.HasFlag(AppArgsFlags.SKIP_VERSION_CHECK);

            if (!runVersionControl)
                return false;

            var appVersionGuard = new ApplicationVersionGuard(staticContainer!.WebRequestsContainer.WebRequestController, bootstrapContainer!.WebBrowser);
            string? latestVersion = await appVersionGuard.GetLatestVersionAsync(ct);

            if (!currentVersion.Version.IsOlderThan(latestVersion))
                return false;

            splashScreen.Hide();

            var appVerRedirectionScreenPrefab = await bootstrapContainer!.AssetsProvisioner!.ProvideMainAssetAsync(dynamicSettings.AppVerRedirectionScreenPrefab, ct);

            ControllerBase<LauncherRedirectionScreenView, ControllerNoData>.ViewFactoryMethod authScreenFactory =
                LauncherRedirectionScreenController.CreateLazily(appVerRedirectionScreenPrefab.Value.GetComponent<LauncherRedirectionScreenView>(), null);

            var launcherRedirectionScreenController = new LauncherRedirectionScreenController(appVersionGuard, authScreenFactory, currentVersion.Version, latestVersion);
            dynamicWorldContainer!.MvcManager.RegisterController(launcherRedirectionScreenController);

            await dynamicWorldContainer!.MvcManager.ShowAsync(LauncherRedirectionScreenController.IssueCommand(), ct);
            return true;
        }

        private void DisableInputs()
        {
            // We disable Inputs directly because otherwise before login (so before the Input component was created and the system that handles it is working)
            // all inputs will be valid, and it allows for weird behaviour, including opening menus that are not ready to be open yet.
            DCLInput dclInput = DCLInput.Instance;

            dclInput.Shortcuts.Disable();
            dclInput.Player.Disable();
            dclInput.Emotes.Disable();
            dclInput.EmoteWheel.Disable();
            dclInput.FreeCamera.Disable();
            dclInput.Camera.Disable();
            dclInput.UI.Disable();
        }

        private void RestoreInputs()
        {
            // We enable Inputs through the inputBlock so the block counters can be properly updated and the component Active flags are up-to-date as well
            // We restore all inputs except EmoteWheel and FreeCamera as they should be disabled by default
            staticContainer!.InputBlock.EnableAll(InputMapComponent.Kind.FREE_CAMERA,
                InputMapComponent.Kind.EMOTE_WHEEL);

            DCLInput.Instance.UI.Enable();
        }

        private static IDiskCache<PartialLoadingState> NewInstancePartialDiskCache(IAppArgs appArgs, RealmLaunchSettings launchSettings)
        {
            if (launchSettings.CurrentMode == LaunchMode.LocalSceneDevelopment)
            {
                ReportHub.Log(ReportData.UNSPECIFIED, "Disk cached disabled while LSD");
                return IDiskCache<PartialLoadingState>.Null.INSTANCE;
            }

            if (appArgs.HasFlag(AppArgsFlags.DISABLE_DISK_CACHE))
            {
                ReportHub.Log(ReportData.UNSPECIFIED, $"Disable disk cache, flag --{AppArgsFlags.DISABLE_DISK_CACHE} is passed");
                return IDiskCache<PartialLoadingState>.Null.INSTANCE;
            }

            var cacheDirectory = CacheDirectory.NewDefaultSubdirectory("partials");
            var filesLock = new FilesLock();

            IDiskCleanUp diskCleanUp;

            if (appArgs.HasFlag(AppArgsFlags.DISABLE_DISK_CACHE_CLEANUP))
            {
                ReportHub.Log(ReportData.UNSPECIFIED, $"Disable disk cache cleanup, flag --{AppArgsFlags.DISABLE_DISK_CACHE_CLEANUP} is passed");
                diskCleanUp = IDiskCleanUp.None.INSTANCE;
            }
            else
                diskCleanUp = new LRUDiskCleanUp(cacheDirectory, filesLock);

            var partialCache = new DiskCache<PartialLoadingState, SerializeMemoryIterator<PartialDiskSerializer.State>>(new DiskCache(cacheDirectory, filesLock, diskCleanUp), new PartialDiskSerializer());
            return partialCache;
        }

        private static IDiskCache NewInstanceDiskCache(IAppArgs appArgs, RealmLaunchSettings launchSettings)
        {
            if (launchSettings.CurrentMode == LaunchMode.LocalSceneDevelopment)
            {
                ReportHub.Log(ReportData.UNSPECIFIED, "Disk cached disabled while LSD");
                return new IDiskCache.Fake();
            }

            if (appArgs.HasFlag(AppArgsFlags.DISABLE_DISK_CACHE))
            {
                ReportHub.Log(ReportData.UNSPECIFIED, $"Disable disk cache, flag --{AppArgsFlags.DISABLE_DISK_CACHE} is passed");
                return new IDiskCache.Fake();
            }

            var cacheDirectory = CacheDirectory.NewDefault();
            var filesLock = new FilesLock();

            IDiskCleanUp diskCleanUp;

            if (appArgs.HasFlag(AppArgsFlags.DISABLE_DISK_CACHE_CLEANUP))
            {
                ReportHub.Log(ReportData.UNSPECIFIED, $"Disable disk cache cleanup, flag --{AppArgsFlags.DISABLE_DISK_CACHE_CLEANUP} is passed");
                diskCleanUp = IDiskCleanUp.None.INSTANCE;
            }
            else
                diskCleanUp = new LRUDiskCleanUp(cacheDirectory, filesLock);

            var diskCache = new DiskCache(cacheDirectory, filesLock, diskCleanUp);
            return diskCache;
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

        private async UniTask<bool> IsTrustedRealmAsync(DecentralandUrlsSource dclUrls, CancellationToken ct)
        {
            if (launchSettings.initialRealm != InitialRealm.Custom) return true;
            if (launchSettings.CurrentMode == LaunchMode.LocalSceneDevelopment) return true;

            string realm = launchSettings.customRealm;

            if (string.IsNullOrEmpty(realm)) return true;

            var uri = new Uri(realm);
            if (uri.Host == "127.0.0.1") return true;
            if (uri.Host == "localhost") return true;
            if (uri.Host == "sdk-team-cdn.decentraland.org") return true;
            if (uri.Host == "sdk-test-scenes.decentraland.zone") return true;
            if (uri.Host == "realm-provider-ea.decentraland.org") return true;
            if (uri.Host == "realm-provider-ea.decentraland.zone") return true;
            if (uri.Host == "worlds-content-server.decentraland.org") return true;

            IWebRequestController webRequestController = staticContainer!.WebRequestsContainer.WebRequestController;

            // If we want to save one http request, we could have a hardcoded list of trusted realms instead
            var url = URLAddress.FromString(dclUrls.Url(DecentralandUrl.Servers));
            var adapter = webRequestController.GetAsync(new CommonArguments(url), ct, ReportCategory.REALM);
            TrustedRealmApiResponse[] realms = await adapter.CreateFromJson<TrustedRealmApiResponse[]>(WRJsonParser.Newtonsoft);

            foreach (TrustedRealmApiResponse trustedRealm in realms)
                if (string.Equals(trustedRealm.baseUrl, realm, StringComparison.OrdinalIgnoreCase))
                    return true;

            return false;
        }

        private async UniTask<bool> ShowUntrustedRealmConfirmationAsync(CancellationToken ct)
        {
            var prefab = await bootstrapContainer!.AssetsProvisioner!.ProvideMainAssetAsync(untrustedRealmConfirmationPrefab, ct);

            UntrustedRealmConfirmationController controller = new UntrustedRealmConfirmationController(
                UntrustedRealmConfirmationController.CreateLazily(prefab.Value.GetComponent<UntrustedRealmConfirmationView>(), null));

            IMVCManager mvcManager = dynamicWorldContainer!.MvcManager;
            mvcManager.RegisterController(controller);

            var args = new UntrustedRealmConfirmationController.Args { realm = launchSettings.customRealm };
            await mvcManager.ShowAsync(UntrustedRealmConfirmationController.IssueCommand(args), ct);

            return controller.SelectedOption;
        }

        [Serializable]
        public struct TrustedRealmApiResponse
        {
            public string baseUrl;
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
