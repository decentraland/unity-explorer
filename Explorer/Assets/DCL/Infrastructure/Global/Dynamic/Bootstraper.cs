using Arch.Core;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Audio;
using DCL.Chat.History;
using DCL.DebugUtilities;
using DCL.Diagnostics;
using DCL.FeatureFlags;
using DCL.Ipfs;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Notifications.NewNotification;
using DCL.Optimization.PerformanceBudgeting;
using DCL.PerformanceAndDiagnostics.Analytics;
using DCL.PerformanceAndDiagnostics.DotNetLogging;
using DCL.PluginSystem;
using DCL.PluginSystem.Global;
using DCL.RealmNavigation;
using DCL.SceneLoadingScreens.SplashScreen;
using DCL.UI.MainUI;
using DCL.UserInAppInitializationFlow;
using DCL.Utilities.Extensions;
using DCL.Utility;
using DCL.Web3.Authenticators;
using DCL.Web3.Identities;
using DCL.WebRequests.Analytics;
using ECS;
using ECS.StreamableLoading.Cache.Disk;
using ECS.StreamableLoading.Cache.InMemory;
using ECS.StreamableLoading.Common.Components;
using Global.AppArgs;
using Global.Dynamic.RealmUrl;
using Global.Versioning;
using MVC;
using SceneRunner.Debugging;
using SceneRuntime.Factory.JsSource;
using SceneRuntime.Factory.WebSceneSource;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.UIElements;
using Utility;
using JsCodeResolver = DCL.AssetsProvision.CodeResolver.JsCodeResolver;

namespace Global.Dynamic
{
    public class Bootstrap : IBootstrap
    {
        private readonly DebugSettings.DebugSettings debugSettings;
        private readonly RealmUrls realmUrls;
        private readonly IAppArgs appArgs;
        private readonly SplashScreen splashScreen;
        private readonly RealmLaunchSettings realmLaunchSettings;
        private readonly WebRequestsContainer webRequestsContainer;
        private readonly IDiskCache diskCache;
        private readonly IDiskCache<PartialLoadingState> partialsDiskCache;
        private readonly HttpFeatureFlagsProvider featureFlagsProvider;
        private readonly World world;

        private URLDomain? startingRealm;
        private Vector2Int startingParcel;
        private DynamicWorldDependencies dynamicWorldDependencies;

        public bool EnableAnalytics { private get; init; }

        public Bootstrap(
            DebugSettings.DebugSettings debugSettings,
            IAppArgs appArgs,
            SplashScreen splashScreen,
            RealmUrls realmUrls,
            RealmLaunchSettings realmLaunchSettings,
            WebRequestsContainer webRequestsContainer,
            IDiskCache diskCache,
            IDiskCache<PartialLoadingState> partialsDiskCache,
            HttpFeatureFlagsProvider featureFlagsProvider,
            World world)
        {
            this.debugSettings = debugSettings;
            this.realmUrls = realmUrls;
            this.appArgs = appArgs;
            this.splashScreen = splashScreen;
            this.realmLaunchSettings = realmLaunchSettings;
            this.webRequestsContainer = webRequestsContainer;
            this.diskCache = diskCache;
            this.partialsDiskCache = partialsDiskCache;
            this.world = world;
            this.featureFlagsProvider = featureFlagsProvider;
        }

        public async UniTask PreInitializeSetupAsync(CancellationToken token)
        {
            ReportHub.LogProductionInfo("[BOOT.Pre] 1 - splashScreen.Show");
            splashScreen.Show();

            ReportHub.LogProductionInfo("[BOOT.Pre] 2 - awaiting realmUrls.StartingRealmAsync");
            string realm = await realmUrls.StartingRealmAsync(token);
            ReportHub.LogProductionInfo($"[BOOT.Pre] 3 - StartingRealmAsync returned '{realm}'");
            startingRealm = URLDomain.FromString(realm);

            // Initialize .NET logging ASAP since it might be used by another systems
            // Otherwise we might get exceptions in different platforms
            ReportHub.LogProductionInfo("[BOOT.Pre] 4 - DotNetLoggingPlugin.Initialize");
            DotNetLoggingPlugin.Initialize();
            ReportHub.LogProductionInfo("[BOOT.Pre] 5 - DotNetLoggingPlugin initialized");
        }

        public async UniTask<(StaticContainer?, bool)> LoadStaticContainerAsync(
            BootstrapContainer bootstrapContainer,
            PluginSettingsContainer globalPluginSettingsContainer,
            IDebugContainerBuilder debugContainerBuilder,
            RealmData realmData,
            Entity playerEntity,
            ISystemMemoryCap memoryCap,
            IAppArgs appArgs,
            CancellationToken ct
        ) =>
            await StaticContainer.CreateAsync(
                bootstrapContainer.Analytics,
                bootstrapContainer.DecentralandUrlsSource,
                realmData,
                bootstrapContainer.AssetsProvisioner,
                bootstrapContainer.ReportHandlingSettings,
                debugContainerBuilder,
                webRequestsContainer,
                globalPluginSettingsContainer,
                bootstrapContainer.DiagnosticsContainer,
                bootstrapContainer.IdentityCache,
                bootstrapContainer.CompositeWeb3Provider,
                bootstrapContainer.LaunchMode,
                bootstrapContainer.UseRemoteAssetBundles,
                world,
                playerEntity,
                memoryCap,
                bootstrapContainer.VolumeBus,
                EnableAnalytics,
                diskCache,
                partialsDiskCache,
                bootstrapContainer.Environment,
                ct,
                appArgs
            );

        public async UniTask<(DynamicWorldContainer?, bool)> LoadDynamicWorldContainerAsync(
            BootstrapContainer bootstrapContainer,
            StaticContainer staticContainer,
            PluginSettingsContainer scenePluginSettingsContainer,
            DynamicSceneLoaderSettings settings,
            DynamicSettings dynamicSettings,
            AudioClipConfig backgroundMusic,
            WorldInfoTool worldInfoTool,
            Entity playerEntity,
            IAppArgs appArgs,
            ICoroutineRunner coroutineRunner,
            DCLVersion dclVersion,
            CancellationToken ct)
        {
            dynamicWorldDependencies = new DynamicWorldDependencies
            (
                staticContainer.DebugContainerBuilder,
                appArgs,
                bootstrapContainer.AssetsProvisioner,
                staticContainer,
                scenePluginSettingsContainer,
                dynamicSettings,
                bootstrapContainer.CompositeWeb3Provider!,
                bootstrapContainer.IdentityCache,
                splashScreen,
                worldInfoTool
            );

            string? localSceneDevelopmentRealm = await realmUrls.LocalSceneDevelopmentRealmAsync(ct);

            (DynamicWorldContainer? container, bool success) tuple = await DynamicWorldContainer.CreateAsync(
                bootstrapContainer,
                dynamicWorldDependencies,
                new DynamicWorldParams
                {
                    StaticLoadPositions = realmLaunchSettings.GetPredefinedParcels(),
                    Realms = settings.Realms,
                    StartParcel = new StartParcel(realmLaunchSettings.targetScene),
                    EditorPositionOverrideActive = realmLaunchSettings.HasEditorPositionOverride(),
                    IsolateScenesCommunication = realmLaunchSettings.isolateSceneCommunication,
                    EnableLandscape = debugSettings.EnableLandscape,
                    EnableLOD = debugSettings.EnableLOD && realmLaunchSettings.CurrentMode is LaunchMode.Play,
                    EnableAnalytics = EnableAnalytics,
                    HybridSceneParams = realmLaunchSettings.CreateHybridSceneParams(),
                    LocalSceneDevelopmentRealm = localSceneDevelopmentRealm ?? string.Empty,
                },
                backgroundMusic,
                world,
                playerEntity,
                appArgs,
                coroutineRunner,
                dclVersion,
                realmUrls,
                ct);

            return tuple;
        }

        public async UniTask<bool> InitializePluginsAsync(StaticContainer staticContainer, DynamicWorldContainer dynamicWorldContainer,
            PluginSettingsContainer scenePluginSettingsContainer, PluginSettingsContainer globalPluginSettingsContainer, IAnalyticsController analyticsController,
            CancellationToken ct)
        {
            var anyFailure = false;

            await UniTask.WhenAll(staticContainer.ECSWorldPlugins.Select(gp => scenePluginSettingsContainer.InitializePluginWithAnalyticsAsync(gp, analyticsController, ct).ContinueWith(OnPluginInitialized)).EnsureNotNull());
            await UniTask.WhenAll(dynamicWorldContainer.GlobalPlugins.Select(gp => globalPluginSettingsContainer.InitializePluginWithAnalyticsAsync(gp, analyticsController, ct).ContinueWith(OnPluginInitialized)).EnsureNotNull());

            void OnPluginInitialized<TPluginInterface>((TPluginInterface plugin, bool success) result) where TPluginInterface: IDCLPlugin
            {
                if (!result.success)
                    anyFailure = true;
            }

            return anyFailure;
        }

        public async UniTask InitializeFeatureFlagsAsync(IWeb3Identity? identity, IDecentralandUrlsSource decentralandUrlsSource, CancellationToken ct)
        {
            try { await featureFlagsProvider.InitializeAsync(decentralandUrlsSource, identity?.Address, appArgs, ct); }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                FeatureFlagsConfiguration.Reset();
                FeatureFlagsConfiguration.Initialize(new FeatureFlagsConfiguration(FeatureFlagsResultDto.Empty));
                ReportHub.LogException(e, new ReportData(ReportCategory.FEATURE_FLAGS));
            }
        }

        public void InitializeFeaturesRegistry()
        {
            FeaturesRegistry.Initialize(new FeaturesRegistry(appArgs, realmLaunchSettings.CurrentMode is LaunchMode.LocalSceneDevelopment));

            // Gate the v49 deps-digest cache-keying scheme behind the feature flag. Off by default means every
            // manifest reports SupportsDepsDigests() == false and the entire pipeline takes the legacy code path.
            AssetBundleManifestVersion.DepsDigestKeyingEnabled = FeaturesRegistry.Instance.IsEnabled(FeatureId.AB_DEPS_DIGEST_CACHE_KEY);
        }

        public GlobalWorld CreateGlobalWorld(
            BootstrapContainer bootstrapContainer,
            StaticContainer staticContainer,
            DynamicWorldContainer dynamicWorldContainer,
            UIDocument debugUiRoot,
            Entity playerEntity
        )
        {
            IWebJsSources webJsSources = new WebJsSources(new JsCodeResolver(
                staticContainer.WebRequestsContainer.WebRequestController));

            if (realmLaunchSettings.CurrentMode is LaunchMode.Play)
            {
                var memoryCache = new MemoryCache<string, string>();
                staticContainer.CacheCleaner.Register(memoryCache);
                webJsSources = new CachedWebJsSources(webJsSources, memoryCache, new DiskCache<string, SerializeMemoryIterator<StringDiskSerializer.State>>(diskCache, new StringDiskSerializer()));
            }

            SceneSharedContainer sceneSharedContainer = SceneSharedContainer.Create(
                in staticContainer,
                bootstrapContainer.DecentralandUrlsSource,
                bootstrapContainer.IdentityCache,
                staticContainer.WebRequestsContainer.SceneWebRequestController,
                dynamicWorldContainer.RealmController.RealmData,
                dynamicWorldContainer.ProfileRepository,
                dynamicWorldContainer.RoomHub,
                dynamicWorldContainer.MvcManager,
                dynamicWorldContainer.MessagePipesHub,
                dynamicWorldContainer.RemoteMetadata,
                webJsSources,
                bootstrapContainer.Environment,
                dynamicWorldContainer.SystemClipboard
            );

            GlobalWorld globalWorld = dynamicWorldContainer.GlobalWorldFactory.Create(
                sceneSharedContainer.SceneFactory, playerEntity);

            dynamicWorldContainer.RealmController.GlobalWorld = globalWorld;
            staticContainer.PortableExperiencesController.GlobalWorld = globalWorld;

            InitializeDebugPanel(staticContainer.DebugContainerBuilder, debugUiRoot);

            return globalWorld;
        }

        public void InitializePlayerEntity(StaticContainer staticContainer, Entity playerEntity)
        {
            staticContainer.CharacterContainer.InitializePlayerEntity(world, playerEntity);
        }

        public async UniTask LoadStartingRealmAsync(DynamicWorldContainer dynamicWorldContainer, CancellationToken ct)
        {
            ReportHub.LogProductionInfo("[BOOT.Realm] 1 - awaiting realmUrls.StartingRealmAsync");
            string realm = await realmUrls.StartingRealmAsync(ct);
            ReportHub.LogProductionInfo($"[BOOT.Realm] 2 - resolved starting realm '{realm}'");
            startingRealm = URLDomain.FromString(realm);

            if (startingRealm.HasValue == false)
                throw new InvalidOperationException("Starting realm is not set");

            if (realmLaunchSettings.initialRealm is InitialRealm.World)
            {
                ReportHub.LogProductionInfo("[BOOT.Realm] 3 - awaiting IsUserAuthorisedToAccessWorldAsync (HTTP)");
                bool isAuthorized = await dynamicWorldContainer.RealmController
                    .IsUserAuthorisedToAccessWorldAsync(startingRealm.Value, ct);

                if (!isAuthorized)
                {
                    ReportHub.LogWarning(ReportCategory.REALM,
                        $"[Bootstrap] Startup world '{realmLaunchSettings.TargetWorld}' is not authorized for auto-entry, falling back to Genesis.");

                    dynamicWorldContainer.ChatHistory.AddMessage(
                        ChatChannel.NEARBY_CHANNEL_ID,
                        ChatChannel.ChatChannelType.NEARBY,
                        ChatMessage.NewFromSystem($"Could not auto-enter '{realmLaunchSettings.TargetWorld}' due to world permissions. You were sent to Genesis Plaza."));

                    await dynamicWorldContainer.RealmController
                        .SetRealmAsync(URLDomain.FromString(realmUrls.GenesisRealm()), ct);
                    return;
                }
            }

            ReportHub.LogProductionInfo($"[BOOT.Realm] 4 - awaiting RealmController.SetRealmAsync('{startingRealm.Value}')");
            await dynamicWorldContainer.RealmController.SetRealmAsync(startingRealm.Value, ct);
            ReportHub.LogProductionInfo("[BOOT.Realm] 5 - SetRealmAsync done");
        }

        public void ApplyFeatureFlagConfigs(FeatureFlagsConfiguration featureFlagsConfigurationCache)
        {
            realmLaunchSettings.CheckStartParcelOverride(appArgs, featureFlagsConfigurationCache);
            webRequestsContainer.SetKTXEnabled(featureFlagsConfigurationCache.IsEnabled(FeatureFlagsStrings.KTX2_CONVERSION));
        }

        public async UniTask UserInitializationAsync(DynamicWorldContainer dynamicWorldContainer,
            BootstrapContainer bootstrapContainer,
            GlobalWorld globalWorld, Entity playerEntity, CancellationToken ct)
        {
            ReportHub.LogProductionInfo("[BOOT.User] 1 - splashScreen.Show");
            splashScreen.Show();

            IWeb3Authenticator authenticator = new TokenFileAuthenticator(
                URLAddress.FromString(bootstrapContainer.DecentralandUrlsSource.Url(DecentralandUrl.ApiAuth)),
                webRequestsContainer.WebRequestController,
                bootstrapContainer.Web3AccountFactory);

            if (EnableAnalytics)
                authenticator = new TrackedTokenFileAuthenticator((TokenFileAuthenticator)authenticator, bootstrapContainer.Analytics.Controller);

            try
            {
                ReportHub.LogProductionInfo("[BOOT.User] 2 - awaiting authenticator.LoginAsync (auto-login token)");
                IWeb3Identity identity = await authenticator.LoginAsync(new LoginPayload(), ct); // doesn't use payload
                ReportHub.LogProductionInfo($"[BOOT.User] 3 - LoginAsync ok, identity={identity.Address}");

                bootstrapContainer.IdentityCache!.Identity = identity;

                if (EnableAnalytics)
                    bootstrapContainer.Analytics.Controller.Identify(identity);
            }
            catch (AutoLoginTokenNotFoundException) { ReportHub.LogProductionInfo("[BOOT.User] 3a - AutoLoginTokenNotFoundException (no cached token; will show auth UI)"); } // Exceptions on auto-login should not block the application bootstrap
            catch (AutoLoginTokenInvalidException e) { ReportHub.LogException(e, ReportCategory.AUTHENTICATION); ReportHub.LogProductionInfo("[BOOT.User] 3b - AutoLoginTokenInvalidException"); }
            catch (Exception e) { ReportHub.LogException(e, ReportCategory.AUTHENTICATION); ReportHub.LogProductionInfo($"[BOOT.User] 3c - LoginAsync threw {e.GetType().Name}"); }

            ReportHub.LogProductionInfo("[BOOT.User] 4 - awaiting UserInAppInAppInitializationFlow.ExecuteAsync");
            await dynamicWorldContainer.UserInAppInAppInitializationFlow.ExecuteAsync(
                new UserInAppInitializationFlowParameters
                (
                    showAuthentication: debugSettings.ShowAuthentication,
                    showLoading: debugSettings.ShowLoading,
                    IUserInAppInitializationFlow.LoadSource.StartUp,
                    world: globalWorld.EcsWorld,
                    playerEntity: playerEntity
                ), ct);
            ReportHub.LogProductionInfo("[BOOT.User] 5 - UserInAppInAppInitializationFlow.ExecuteAsync done");

            OpenDefaultUI(dynamicWorldContainer.MvcManager, ct);
            ReportHub.LogProductionInfo("[BOOT.User] 6 - default UI opened; hiding splash");

            splashScreen.Hide();
        }

        private static void OpenDefaultUI(IMVCManager mvcManager, CancellationToken ct)
        {
            mvcManager.ShowAsync(NewNotificationController.IssueCommand(), ct).Forget();
            mvcManager.ShowAsync(MainUIController.IssueCommand(), ct).Forget();
        }

        private void InitializeDebugPanel(IDebugContainerBuilder debugContainerBuilder, UIDocument debugUiRoot)
        {
            debugContainerBuilder.BuildWithFlex(debugUiRoot);
            bool hasDebugFlag = appArgs.HasDebugFlag();

            // Make Debug Panel available
            debugContainerBuilder.IsVisible = hasDebugFlag || appArgs.HasFlag(AppArgsFlags.LOCAL_SCENE);

            // Start application with Debug Panel open/closed
            debugContainerBuilder.Container.SetPanelVisibility(hasDebugFlag);
        }
    }
}
