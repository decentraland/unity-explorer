using Arch.Core;
using CommunicationData.URLHelpers;
using CRDT;
using CrdtEcsBridge.Components;
using Cysharp.Threading.Tasks;
using DCL.ApplicationBlocklistGuard;
using DCL.ApplicationMinimumSpecsGuard;
using DCL.ApplicationVersionGuard;
using DCL.AssetsProvision;
using DCL.Audio;
using DCL.AuthenticationScreenFlow;
using DCL.Browser;
using DCL.Browser.DecentralandUrls;
using DCL.DebugUtilities;
using DCL.Diagnostics;
using DCL.FeatureFlags;
using DCL.Global.Dynamic;
using DCL.Infrastructure.Global;
using DCL.Input.Component;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Multiplayer.HealthChecks;
using DCL.Multiplayer.HealthChecks.Struct;
using DCL.Optimization.PerformanceBudgeting;
using DCL.PerformanceAndDiagnostics.Analytics;
using DCL.PluginSystem;
using DCL.PluginSystem.Global;
using DCL.Prefs;
using DCL.SceneLoadingScreens.SplashScreen;
using DCL.Settings.ModuleControllers;
using DCL.Settings.Utils;
using DCL.Utilities;
using DCL.Utilities.Extensions;
using DCL.Utility;
using DCL.Utility.Types;
using DCL.Web3.Accounts.Factory;
using DCL.Web3.Identities;
using DCL.WebRequests;
using DCL.WebRequests.Analytics;
using DCL.WebRequests.ChromeDevtool;
using ECS.StreamableLoading.Cache.Disk;
using ECS.StreamableLoading.Cache.Disk.CleanUp;
using ECS.StreamableLoading.Cache.Disk.Lock;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Common.Components;
using Global.AppArgs;
using Global.Dynamic.RealmUrl;
using Global.Dynamic.RealmUrl.Names;
using Global.Versioning;
using MVC;
using SceneRunner.Debugging;
using System;
using System.Linq;
using System.Threading;
using DCL.UI.ErrorPopup;
using DG.Tweening;
using ECS;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.UI;
using Utility;
using MinimumSpecsScreenView = DCL.ApplicationMinimumSpecsGuard.MinimumSpecsScreenView;

namespace Global.Dynamic
{
    public class MainSceneLoader : MonoBehaviour, ICoroutineRunner
    {
        [Header("STARTUP CONFIG")] [SerializeField]
        private RealmLaunchSettings launchSettings = null!;

        [Space]
        [SerializeField] private DecentralandEnvironment decentralandEnvironment;

        [Space]
        [SerializeField] private DebugSettings.DebugSettings debugSettings = new ();

        [Header("REFERENCES")]
        [SerializeField] private PluginSettingsContainer globalPluginSettingsContainer = null!;
        [SerializeField] private PluginSettingsContainer scenePluginSettingsContainer = null!;
        [SerializeField] private DynamicSceneLoaderSettings settings = null!;
        [SerializeField] private SplashScreenRef splashScreenRef = null!;
        [SerializeField] private DynamicSettings dynamicSettings = null!;
        [SerializeField] private AudioClipConfig backgroundMusic = null!;
        [SerializeField] private WorldInfoTool worldInfoTool = null!;
        [SerializeField] private AssetReferenceGameObject untrustedRealmConfirmationPrefab = null!;

        private BootstrapContainer? bootstrapContainer;
        private StaticContainer? staticContainer;
        private DynamicWorldContainer? dynamicWorldContainer;
        private GlobalWorld? globalWorld;
        private ProvidedInstance<SplashScreen> splashScreen;

        private void Awake()
        {
            ReportHub.Log(ReportCategory.ALWAYS, ">>> Awake() called, starting InitializeFlowAsync");
            InitializeFlowAsync(destroyCancellationToken).Forget();
        }

        private void OnDestroy()
        {
            ReportHub.Log(ReportCategory.ALWAYS, ">>> OnDestroy() START");
            DisableAllSelectableTransitions();

            ReportHub.Log(ReportCategory.ALWAYS, ">>> OnDestroy() disposing dynamicWorldContainer plugins...");
            if (dynamicWorldContainer != null)
            {
                foreach (IDCLGlobalPlugin plugin in dynamicWorldContainer.GlobalPlugins)
                    plugin.SafeDispose(ReportCategory.ENGINE);

                if (globalWorld != null)
                    dynamicWorldContainer.RealmController.DisposeGlobalWorld();

                dynamicWorldContainer.SafeDispose(ReportCategory.ENGINE);
            }
            ReportHub.Log(ReportCategory.ALWAYS, ">>> OnDestroy() dynamicWorldContainer disposed");

            ReportHub.Log(ReportCategory.ALWAYS, ">>> OnDestroy() disposing staticContainer...");
            if (staticContainer != null)
            {
                // Exclude SharedPlugins as they were disposed as they were already disposed of as `GlobalPlugins`
                foreach (IDCLPlugin worldPlugin in staticContainer.ECSWorldPlugins.Except<IDCLPlugin>(staticContainer.SharedPlugins))
                    worldPlugin.SafeDispose(ReportCategory.ENGINE);

                staticContainer.SafeDispose(ReportCategory.ENGINE);
            }
            ReportHub.Log(ReportCategory.ALWAYS, ">>> OnDestroy() staticContainer disposed");

            ReportHub.Log(ReportCategory.ALWAYS, ">>> OnDestroy() disposing bootstrapContainer...");
            bootstrapContainer?.Dispose();
            splashScreen.Dispose();

            ReportHub.Log(ReportCategory.ENGINE, "OnDestroy successfully finished");
        }

        private void OnApplicationQuit()
        {
            ReportHub.Log(ReportCategory.ALWAYS, ">>> OnApplicationQuit() called");
            DisableAllSelectableTransitions();
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
            ReportHub.Log(ReportCategory.ALWAYS, "F.0 InitializeFlowAsync START");
            IAppArgs applicationParametersParser = new ApplicationParametersParser(
#if UNITY_EDITOR
                debugSettings.AppParameters
#else
                Environment.GetCommandLineArgs()
#endif
            );
            ReportHub.Log(ReportCategory.ALWAYS, "F.1 ApplicationParametersParser created");

            FeatureFlagsConfiguration.Initialize(new FeatureFlagsConfiguration(FeatureFlagsResultDto.Empty));
            ReportHub.Log(ReportCategory.ALWAYS, "F.2 FeatureFlagsConfiguration initialized (empty)");

            DCLVersion dclVersion = DCLVersion.FromAppArgs(applicationParametersParser);
            DiagnosticInfoUtils.LogSystem(dclVersion.Version);
            ReportHub.Log(ReportCategory.ALWAYS, $"F.3 DCLVersion={dclVersion.Version}");

            // Memory limit
            bool hasSimulatedMemory = applicationParametersParser.TryGetValue(AppArgsFlags.SIMULATE_MEMORY, out string simulatedMemory);
            int systemMemory = hasSimulatedMemory ? int.Parse(simulatedMemory) : SystemInfo.systemMemorySize;

            ISystemMemoryCap memoryCap = hasSimulatedMemory
                ? new SystemMemoryCap(systemMemory)
                : new SystemMemoryCap();
            ReportHub.Log(ReportCategory.ALWAYS, $"F.4 MemoryCap created, systemMemory={systemMemory}, simulated={hasSimulatedMemory}");

            ApplyConfig(applicationParametersParser);
            ReportHub.Log(ReportCategory.ALWAYS, "F.5 ApplyConfig done");

            launchSettings.ApplyConfig(applicationParametersParser);
            ReportHub.Log(ReportCategory.ALWAYS, "F.6 launchSettings.ApplyConfig done");

            if (applicationParametersParser.HasFlag(AppArgsFlags.WINDOWED_MODE))
            {
                ReportHub.Log(ReportCategory.ALWAYS, "F.7 Applying windowed mode");
                WindowModeUtils.ApplyWindowedMode();
            }

            World world = World.Create();
            ReportHub.Log(ReportCategory.ALWAYS, "F.8 ECS World created");

            var realmData = new RealmData();
            var decentralandUrlsSource = new DecentralandUrlsSource(decentralandEnvironment, realmData, launchSettings);
            DiagnosticInfoUtils.LogEnvironment(decentralandUrlsSource);
            ReportHub.Log(ReportCategory.ALWAYS, $"F.9 DecentralandUrlsSource created, env={decentralandEnvironment}");

            var assetsProvisioner = new AddressablesProvisioner();
            ReportHub.Log(ReportCategory.ALWAYS, "F.10 AddressablesProvisioner created");

            ReportHub.Log(ReportCategory.ALWAYS, "F.11 Awaiting splashScreen ProvideInstanceAsync...");
            splashScreen = (await assetsProvisioner.ProvideInstanceAsync(splashScreenRef, ct: ct));
            ReportHub.Log(ReportCategory.ALWAYS, "F.12 splashScreen loaded OK");

            var web3AccountFactory = new Web3AccountFactory();
            ReportHub.Log(ReportCategory.ALWAYS, "F.13 Web3AccountFactory created");

            var identityCache = new IWeb3IdentityCache.Default(web3AccountFactory, decentralandEnvironment);
            ReportHub.Log(ReportCategory.ALWAYS, "F.14 IWeb3IdentityCache created");

            ReportHub.Log(ReportCategory.ALWAYS, "F.15 Awaiting DebugViewsCatalog asset...");
            var debugViewsCatalog = (await assetsProvisioner.ProvideMainAssetAsync(dynamicSettings.DebugViewsCatalog, ct)).Value;
            ReportHub.Log(ReportCategory.ALWAYS, "F.16 DebugViewsCatalog loaded");

            var debugContainer = DebugUtilitiesContainer.Create(debugViewsCatalog, applicationParametersParser.HasDebugFlag(), applicationParametersParser.HasFlag(AppArgsFlags.LOCAL_SCENE));
            ReportHub.Log(ReportCategory.ALWAYS, "F.17 DebugUtilitiesContainer created");

            var diskCache = NewInstanceDiskCache(applicationParametersParser, launchSettings);
            ReportHub.Log(ReportCategory.ALWAYS, "F.18 DiskCache created");

            var partialsDiskCache = NewInstancePartialDiskCache(applicationParametersParser, launchSettings);
            ReportHub.Log(ReportCategory.ALWAYS, "F.19 PartialsDiskCache created");

            ReportHub.Log(ReportCategory.ALWAYS, "F.20 Awaiting BootstrapContainer.CreateAsync...");
            bootstrapContainer = await BootstrapContainer.CreateAsync(
                assetsProvisioner,
                debugSettings,
                sceneLoaderSettings: settings,
                decentralandUrlsSource,
                debugContainer,
                identityCache,
                globalPluginSettingsContainer,
                launchSettings,
                applicationParametersParser,
                splashScreen.Value,
                diskCache,
                partialsDiskCache,
                world,
                decentralandEnvironment,
                dclVersion,
                destroyCancellationToken
            );
            ReportHub.Log(ReportCategory.ALWAYS, "F.21 BootstrapContainer created OK");

            IBootstrap bootstrap = bootstrapContainer!.Bootstrap!;
            ReportHub.Log(ReportCategory.ALWAYS, $"F.22 Bootstrap obtained, type={bootstrap.GetType().Name}");

            try
            {
                ReportHub.Log(ReportCategory.ALWAYS, "F.23 Awaiting PreInitializeSetupAsync...");
                await bootstrap.PreInitializeSetupAsync(destroyCancellationToken);
                ReportHub.Log(ReportCategory.ALWAYS, "F.24 PreInitializeSetupAsync DONE");

                Entity playerEntity = world.Create(new CRDTEntity(SpecialEntitiesID.PLAYER_ENTITY));
                ReportHub.Log(ReportCategory.ALWAYS, "F.25 PlayerEntity created");

                ReportHub.Log(ReportCategory.ALWAYS, "F.26 Awaiting InitializeFeatureFlagsAsync...");
                await bootstrap.InitializeFeatureFlagsAsync(bootstrapContainer.IdentityCache!.Identity,
                    bootstrapContainer.DecentralandUrlsSource, ct);
                ReportHub.Log(ReportCategory.ALWAYS, "F.27 InitializeFeatureFlagsAsync DONE");

                bootstrap.InitializeFeaturesRegistry();
                ReportHub.Log(ReportCategory.ALWAYS, "F.28 InitializeFeaturesRegistry DONE");

                bootstrap.ApplyFeatureFlagConfigs(FeatureFlagsConfiguration.Instance);
                ReportHub.Log(ReportCategory.ALWAYS, "F.29 ApplyFeatureFlagConfigs DONE");

                ReportHub.Log(ReportCategory.ALWAYS, "F.30 Awaiting LoadStaticContainerAsync...");
                bool isLoaded;
                (staticContainer, isLoaded) = await bootstrap.LoadStaticContainerAsync(bootstrapContainer, globalPluginSettingsContainer, debugContainer.Builder, realmData, playerEntity, memoryCap, applicationParametersParser, ct);

                if (!isLoaded)
                {
                    ReportHub.Log(ReportCategory.ALWAYS, "F.31 LoadStaticContainerAsync FAILED — DEAD");
                    GameReports.PrintIsDead();
                    return;
                }
                ReportHub.Log(ReportCategory.ALWAYS, "F.31 LoadStaticContainerAsync DONE OK");

                bootstrap.InitializePlayerEntity(staticContainer!, playerEntity);
                ReportHub.Log(ReportCategory.ALWAYS, "F.32 InitializePlayerEntity DONE");

                staticContainer!.SceneLoadingLimit.SetEnabled(FeatureFlagsConfiguration.Instance.IsEnabled(FeatureFlagsStrings.SCENE_MEMORY_LIMIT));
                ReportHub.Log(ReportCategory.ALWAYS, "F.33 SceneLoadingLimit set");

                OfficialWalletsHelper.Initialize(new OfficialWalletsHelper());
                ReportHub.Log(ReportCategory.ALWAYS, "F.34 OfficialWalletsHelper initialized");

                ReportHub.Log(ReportCategory.ALWAYS, "F.35 Awaiting LoadDynamicWorldContainerAsync...");
                (dynamicWorldContainer, isLoaded) = await bootstrap.LoadDynamicWorldContainerAsync(
                    bootstrapContainer,
                    staticContainer!,
                    scenePluginSettingsContainer,
                    settings,
                    dynamicSettings,
                    backgroundMusic,
                    worldInfoTool.EnsureNotNull(),
                    playerEntity,
                    applicationParametersParser,
                    coroutineRunner: this,
                    dclVersion,
                    destroyCancellationToken);

                if (!isLoaded)
                {
                    ReportHub.Log(ReportCategory.ALWAYS, "F.36 LoadDynamicWorldContainerAsync FAILED — DEAD");
                    GameReports.PrintIsDead();
                    return;
                }
                ReportHub.Log(ReportCategory.ALWAYS, "F.36 LoadDynamicWorldContainerAsync DONE OK");

                ReportHub.Log(ReportCategory.ALWAYS, "F.37 Awaiting InitialGuardsCheckSuccessAsync...");
                if (!await InitialGuardsCheckSuccessAsync(applicationParametersParser, decentralandUrlsSource, ct))
                {
                    ReportHub.Log(ReportCategory.ALWAYS, "F.38 InitialGuardsCheckSuccessAsync FAILED — returning");
                    return;
                }
                ReportHub.Log(ReportCategory.ALWAYS, "F.38 InitialGuardsCheckSuccessAsync DONE OK");

                ReportHub.Log(ReportCategory.ALWAYS, "F.39 Awaiting VerifyMinimumHardwareRequirementMetAsync...");
                await VerifyMinimumHardwareRequirementMetAsync(applicationParametersParser, bootstrapContainer.WebBrowser, bootstrapContainer.Analytics, ct);
                ReportHub.Log(ReportCategory.ALWAYS, "F.40 VerifyMinimumHardwareRequirementMetAsync DONE");

                ReportHub.Log(ReportCategory.ALWAYS, "F.41 Awaiting IsTrustedRealmAsync...");
                if (!await IsTrustedRealmAsync(decentralandUrlsSource, ct))
                {
                    ReportHub.Log(ReportCategory.ALWAYS, "F.42 Realm is NOT trusted — showing confirmation");
                    splashScreen.Value.Hide();

                    if (!await ShowUntrustedRealmConfirmationAsync(ct))
                    {
                        ReportHub.Log(ReportCategory.ALWAYS, "F.43 User rejected untrusted realm — exiting");
                        ExitUtils.Exit();
                        return;
                    }

                    ReportHub.Log(ReportCategory.ALWAYS, "F.43 User accepted untrusted realm");
                    splashScreen.Value.Show();
                }
                ReportHub.Log(ReportCategory.ALWAYS, "F.44 IsTrustedRealmAsync DONE");

                DisableInputs();
                ReportHub.Log(ReportCategory.ALWAYS, "F.45 Inputs disabled");

                ReportHub.Log(ReportCategory.ALWAYS, "F.46 Awaiting InitializePluginsAsync...");
                if (await bootstrap.InitializePluginsAsync(staticContainer!, dynamicWorldContainer!, scenePluginSettingsContainer, globalPluginSettingsContainer, bootstrapContainer.Analytics, ct))
                {
                    ReportHub.Log(ReportCategory.ALWAYS, "F.47 InitializePluginsAsync FAILED — DEAD");
                    GameReports.PrintIsDead();
                    return;
                }
                ReportHub.Log(ReportCategory.ALWAYS, "F.47 InitializePluginsAsync DONE OK");

                ReportHub.Log(ReportCategory.ALWAYS, "F.48 Creating GlobalWorld...");
                globalWorld = bootstrap.CreateGlobalWorld(bootstrapContainer, staticContainer!, dynamicWorldContainer!, debugContainer.RootDocument, playerEntity);
                ReportHub.Log(ReportCategory.ALWAYS, "F.49 GlobalWorld created OK");

                ReportHub.Log(ReportCategory.ALWAYS, "F.50 Awaiting LoadStartingRealmAsync...");
                await LoadStartingRealmAsync(ct);
                ReportHub.Log(ReportCategory.ALWAYS, "F.51 LoadStartingRealmAsync DONE");

                ReportHub.Log(ReportCategory.ALWAYS, "F.52 Awaiting LoadUserFlowAsync (auth + user init)...");
                await LoadUserFlowAsync(playerEntity, ct);
                ReportHub.Log(ReportCategory.ALWAYS, "F.53 LoadUserFlowAsync DONE");

                //This is done to release the memory usage of the splash screen logo animation sprites
                //The logo is used only at first launch, so we can safely release it after the game is loaded
                splashScreen.Dispose();
                ReportHub.Log(ReportCategory.ALWAYS, "F.54 splashScreen disposed");

                RestoreInputs();
                ReportHub.Log(ReportCategory.ALWAYS, "F.55 Inputs restored — FLOW COMPLETE");
            }
            catch (OperationCanceledException)
            {
                ReportHub.Log(ReportCategory.ALWAYS, "F.ERR OperationCanceledException caught — flow cancelled");
            }
            catch (Exception e)
            {
                ReportHub.Log(ReportCategory.ALWAYS, $"F.ERR Unhandled exception: {e.GetType().Name}: {e.Message}");
                GameReports.PrintIsDead();
                throw;
            }

            return;

            async UniTask LoadStartingRealmAsync(CancellationToken ct)
            {
                ReportHub.Log(ReportCategory.ALWAYS, "F.50.1 LoadStartingRealmAsync inner START");
                try
                {
                    await bootstrap.LoadStartingRealmAsync(dynamicWorldContainer!, ct);
                    ReportHub.Log(ReportCategory.ALWAYS, "F.50.2 LoadStartingRealmAsync inner DONE OK");
                }
                catch (RealmChangeException e)
                {
                    ReportHub.Log(ReportCategory.ALWAYS, $"F.50.3 RealmChangeException: {e.Message}");
                    if (await ShowLoadErrorPopupAsync(ct) == ErrorPopupWithRetryController.Result.RESTART)
                        await LoadStartingRealmAsync(ct);
                    else
                        ExitUtils.Exit();
                }
            }

            async UniTask LoadUserFlowAsync(Entity playerEntity, CancellationToken ct)
            {
                ReportHub.Log(ReportCategory.ALWAYS, "F.52.1 LoadUserFlowAsync inner START — calling UserInitializationAsync");
                try
                {
                    await bootstrap.UserInitializationAsync(dynamicWorldContainer!, bootstrapContainer, globalWorld, playerEntity, ct);
                    ReportHub.Log(ReportCategory.ALWAYS, "F.52.2 UserInitializationAsync DONE OK");
                }
                catch (TimeoutException e)
                {
                    ReportHub.Log(ReportCategory.ALWAYS, $"F.52.3 TimeoutException: {e.Message}");
                    if (await ShowLoadErrorPopupAsync(ct) == ErrorPopupWithRetryController.Result.RESTART)
                        await LoadUserFlowAsync(playerEntity, ct);
                    else
                        throw;
                }
                catch (RealmChangeException e)
                {
                    ReportHub.Log(ReportCategory.ALWAYS, $"F.52.4 RealmChangeException: {e.Message}");
                    if (await ShowLoadErrorPopupAsync(ct) == ErrorPopupWithRetryController.Result.RESTART)
                        await LoadUserFlowAsync(playerEntity, ct);
                    else
                        ExitUtils.Exit();
                }
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
            ReportHub.Log(ReportCategory.ALWAYS, "M.0 VerifyMinimumHardwareRequirementMetAsync START");
            var minimumSpecsGuard = new MinimumSpecsGuard(new DefaultSpecProfileProvider(),
                new UnitySystemInfoProvider(),
                new PlatformDriveInfoProvider());
            ReportHub.Log(ReportCategory.ALWAYS, "M.1 MinimumSpecsGuard created");

            bool hasMinimumSpecs = minimumSpecsGuard.HasMinimumSpecs();
            ReportHub.Log(ReportCategory.ALWAYS, $"M.2 hasMinimumSpecs={hasMinimumSpecs}");

            if (!hasMinimumSpecs)
            {
                ReportHub.Log(ReportCategory.ALWAYS, "M.3.1 Setting low graphics quality (SETTINGS_GRAPHICS_QUALITY)");
                DCLPlayerPrefs.SetInt(DCLPrefKeys.SETTINGS_GRAPHICS_QUALITY, GraphicsQualitySettingsController.MIN_SPECS_GRAPHICS_QUALITY_LEVEL, true);
                ReportHub.Log(ReportCategory.ALWAYS, "M.3.2 Setting upscaler value (SETTINGS_UPSCALER)");
                DCLPlayerPrefs.SetFloat(DCLPrefKeys.SETTINGS_UPSCALER, UpscalingController.MIN_SPECS_UPSCALER_VALUE, true);
                ReportHub.Log(ReportCategory.ALWAYS, "M.3.3 Low quality prefs set");
            }

            bool userWantsToSkip = DCLPlayerPrefs.GetBool(DCLPrefKeys.DONT_SHOW_MIN_SPECS_SCREEN);
            ReportHub.Log(ReportCategory.ALWAYS, $"M.4 userWantsToSkip={userWantsToSkip}");

            bool forceShow = applicationParametersParser.HasFlag(AppArgsFlags.FORCE_MINIMUM_SPECS_SCREEN);
            ReportHub.Log(ReportCategory.ALWAYS, $"M.5 forceShow={forceShow}");

            ReportHub.Log(ReportCategory.ALWAYS, "M.6 Setting Sentry scope (AddMeetMinimumRequirements)");
            bootstrapContainer.DiagnosticsContainer.AddSentryScopeConfigurator(scope => { bootstrapContainer.DiagnosticsContainer.Sentry!.AddMeetMinimumRequirements(scope, hasMinimumSpecs); });
            ReportHub.Log(ReportCategory.ALWAYS, "M.7 Sentry scope set OK");

            bool shouldShowScreen = forceShow || (!userWantsToSkip && !hasMinimumSpecs);
            ReportHub.Log(ReportCategory.ALWAYS, $"M.8 shouldShowScreen={shouldShowScreen} (forceShow={forceShow} || (!userWantsToSkip={!userWantsToSkip} && !hasMinimumSpecs={!hasMinimumSpecs}))");

            if (!shouldShowScreen)
            {
                ReportHub.Log(ReportCategory.ALWAYS, "M.9 SKIPPING min specs screen — returning immediately");
                return;
            }

            ReportHub.Log(ReportCategory.ALWAYS, "M.10 Loading MinimumSpecsScreenPrefab...");
            var minimumRequirementsPrefab = await bootstrapContainer!
                                                 .AssetsProvisioner!
                                                 .ProvideMainAssetAsync(dynamicSettings.MinimumSpecsScreenPrefab, ct);
            ReportHub.Log(ReportCategory.ALWAYS, "M.11 MinimumSpecsScreenPrefab loaded OK");

            ControllerBase<MinimumSpecsScreenView, ControllerNoData>.ViewFactoryMethod viewFactory = MinimumSpecsScreenController
               .CreateLazily(minimumRequirementsPrefab.Value.GetComponent<MinimumSpecsScreenView>(), null);

            var minimumSpecsResults = minimumSpecsGuard.Results;
            var minimumSpecsScreenController = new MinimumSpecsScreenController(viewFactory, webBrowser, analytics, minimumSpecsResults);
            ReportHub.Log(ReportCategory.ALWAYS, "M.12 MinimumSpecsScreenController created");

            dynamicWorldContainer!.MvcManager.RegisterController(minimumSpecsScreenController);
            ReportHub.Log(ReportCategory.ALWAYS, "M.13 Controller registered in MVC");

            ReportHub.Log(ReportCategory.ALWAYS, "M.14 ShowAsync MinimumSpecsScreen — AWAITING user to press Continue...");
            dynamicWorldContainer!.MvcManager.ShowAsync(MinimumSpecsScreenController.IssueCommand(), ct).Forget();
            await minimumSpecsScreenController.HoldingTask.Task;
            ReportHub.Log(ReportCategory.ALWAYS, "M.15 User dismissed MinimumSpecsScreen — continuing");
        }

        private async UniTask<bool> InitialGuardsCheckSuccessAsync(IAppArgs applicationParametersParser, DecentralandUrlsSource dclSources,
            CancellationToken ct)
        {
            ReportHub.Log(ReportCategory.ALWAYS, "G.0 InitialGuardsCheckSuccessAsync START");

            ReportHub.Log(ReportCategory.ALWAYS, "G.1 Checking IsLivekitDeadAsync...");
            if (await IsLIvekitDeadAsync(staticContainer!.WebRequestsContainer.WebRequestController, dclSources, ct))
            {
                ReportHub.Log(ReportCategory.ALWAYS, "G.2 Livekit is DEAD — returning false");
                return false;
            }
            ReportHub.Log(ReportCategory.ALWAYS, "G.2 Livekit is alive OK");

            ReportHub.Log(ReportCategory.ALWAYS, "G.3 Checking DoesApplicationRequireVersionUpdateAsync...");
            if (await DoesApplicationRequireVersionUpdateAsync(applicationParametersParser, splashScreen.Value, ct))
            {
                ReportHub.Log(ReportCategory.ALWAYS, "G.4 Version update required — returning false");
                return false;
            }
            ReportHub.Log(ReportCategory.ALWAYS, "G.4 No version update needed");

            ReportHub.Log(ReportCategory.ALWAYS, "G.5 Registering BlockedPopup...");
            await RegisterBlockedPopupAsync(bootstrapContainer!.WebBrowser, ct);
            ReportHub.Log(ReportCategory.ALWAYS, "G.6 BlockedPopup registered — returning true");

            return true;
        }

        private async UniTask<bool> IsLIvekitDeadAsync(IWebRequestController webRequestController, DecentralandUrlsSource decentralandUrlsSource, CancellationToken ct)
        {
            ReportHub.Log(ReportCategory.ALWAYS, "G.1.1 IsLivekitDeadAsync — checking Archipelago+Gatekeeper health...");
            SequentialHealthCheck healthCheck = new SequentialHealthCheck(
                new MultipleURLHealthCheck(webRequestController, decentralandUrlsSource,
                    DecentralandUrl.ArchipelagoStatus,
                    DecentralandUrl.GatekeeperStatus
                ).WithRetries(3));

            Result result = await healthCheck.IsRemoteAvailableAsync(ct);
            ReportHub.Log(ReportCategory.ALWAYS, $"G.1.2 HealthCheck result: success={result.Success}");

            if (result.Success) return false;

            ReportHub.Log(ReportCategory.ALWAYS, "G.1.3 Livekit is DOWN — showing LivekitDown screen");
            var livekitDownPrefab = await bootstrapContainer!.AssetsProvisioner!.ProvideMainAssetAsync(dynamicSettings.LivekitDownPrefab, ct);

            ControllerBase<LivekitHealthGuardView, ControllerNoData>.ViewFactoryMethod viewFactory =
                LivekitHealtGuardController.CreateLazily(livekitDownPrefab.Value.GetComponent<LivekitHealthGuardView>(), null);

            dynamicWorldContainer!.MvcManager.RegisterController(new LivekitHealtGuardController(viewFactory));
            dynamicWorldContainer!.MvcManager.ShowAsync(LivekitHealtGuardController.IssueCommand(), ct).Forget();
            return true;
        }

        private async UniTask<bool> DoesApplicationRequireVersionUpdateAsync(IAppArgs applicationParametersParser, SplashScreen splashScreen, CancellationToken ct)
        {
            DCLVersion currentVersion = DCLVersion.FromAppArgs(applicationParametersParser);
            bool runVersionControl = debugSettings.EnableVersionUpdateGuard;
            ReportHub.Log(ReportCategory.ALWAYS, $"G.3.1 VersionCheck: currentVersion={currentVersion.Version}, enableGuard={runVersionControl}");

            if (!Application.isEditor)
            {
                bool hasDebug = applicationParametersParser.HasDebugFlag();
                bool skipCheck = applicationParametersParser.HasFlag(AppArgsFlags.SKIP_VERSION_CHECK);
                bool autopilot = applicationParametersParser.HasFlag(AppArgsFlags.AUTOPILOT);
                runVersionControl = !hasDebug && !skipCheck && !autopilot;
                ReportHub.Log(ReportCategory.ALWAYS, $"G.3.2 VersionCheck override: hasDebug={hasDebug}, skipCheck={skipCheck}, autopilot={autopilot}, runVersionControl={runVersionControl}");
            }

            if (!runVersionControl)
            {
                ReportHub.Log(ReportCategory.ALWAYS, "G.3.3 VersionCheck skipped");
                return false;
            }

            var appVersionGuard = new ApplicationVersionGuard(staticContainer!.WebRequestsContainer.WebRequestController, bootstrapContainer!.WebBrowser);
            ReportHub.Log(ReportCategory.ALWAYS, "G.3.4 Fetching latest version...");
            string? latestVersion = await appVersionGuard.GetLatestVersionAsync(ct);
            ReportHub.Log(ReportCategory.ALWAYS, $"G.3.5 LatestVersion={latestVersion}");

            if (!currentVersion.Version.IsOlderThan(latestVersion))
            {
                ReportHub.Log(ReportCategory.ALWAYS, "G.3.6 Current version is up-to-date");
                return false;
            }

            ReportHub.Log(ReportCategory.ALWAYS, "G.3.7 Version is OUTDATED — showing update screen");
            splashScreen.Hide();

            var appVerRedirectionScreenPrefab = await bootstrapContainer!.AssetsProvisioner!.ProvideMainAssetAsync(dynamicSettings.AppVerRedirectionScreenPrefab, ct);

            ControllerBase<LauncherRedirectionScreenView, ControllerNoData>.ViewFactoryMethod authScreenFactory =
                LauncherRedirectionScreenController.CreateLazily(appVerRedirectionScreenPrefab.Value.GetComponent<LauncherRedirectionScreenView>(), null);

            var launcherRedirectionScreenController = new LauncherRedirectionScreenController(appVersionGuard, authScreenFactory, currentVersion.Version, latestVersion);
            dynamicWorldContainer!.MvcManager.RegisterController(launcherRedirectionScreenController);

            await dynamicWorldContainer!.MvcManager.ShowAsync(LauncherRedirectionScreenController.IssueCommand(), ct);
            ReportHub.Log(ReportCategory.ALWAYS, "G.3.8 Update screen dismissed");
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

        private async UniTask<ErrorPopupWithRetryController.Result> ShowLoadErrorPopupAsync(CancellationToken ct)
        {
            IMVCManager mvcManager = dynamicWorldContainer!.MvcManager;

            var input = new ErrorPopupWithRetryController.Input(
                title: "Load Error",
                description: "A loading error was encountered. Please reload to try again.",
                iconType: ErrorPopupWithRetryController.IconType.ERROR);

            await mvcManager.ShowAsync(ErrorPopupWithRetryController.IssueCommand(input), ct);

            return input.SelectedOption;
        }

        /// <summary>
        /// Required to fix crash on exit, ticket - https://github.com/decentraland/unity-explorer/issues/6180
        /// </summary>
        private static void DisableAllSelectableTransitions()
        {
            DOTween.KillAll();
            Selectable[] all = FindObjectsByType<Selectable>(FindObjectsInactive.Include, FindObjectsSortMode.None) ?? Array.Empty<Selectable>();

            foreach (var s in all)
            {
                // Prevent Unity from executing DestroyTween / StartTween during shutdown
                s.transition = Selectable.Transition.None;

                // Disable any graphic tween still in progress
                Graphic? g = s.targetGraphic;

                if (g != null)
                    g.CrossFadeColor(g.color, 0f, false, false); // instantly settle
            }
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

        [Serializable]
        public class SplashScreenRef : ComponentReference<SplashScreen>
        {
            public SplashScreenRef(string guid) : base(guid) { }
        }
    }
}
