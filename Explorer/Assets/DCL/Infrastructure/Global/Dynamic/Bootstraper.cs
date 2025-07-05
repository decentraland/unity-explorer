using Arch.Core;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Audio;
using DCL.DebugUtilities;
using DCL.Diagnostics;
using DCL.FeatureFlags;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Notifications.NewNotification;
using DCL.Optimization.PerformanceBudgeting;
using DCL.PerformanceAndDiagnostics.DotNetLogging;
using DCL.PluginSystem;
using DCL.PluginSystem.Global;
using DCL.PluginSystem.World;
using DCL.Profiles;
using DCL.RealmNavigation;
using DCL.SceneLoadingScreens.SplashScreen;
using DCL.UI.MainUI;
using DCL.UserInAppInitializationFlow;
using DCL.Utilities;
using DCL.Utilities.Extensions;
using DCL.Web3.Identities;
using DCL.WebRequests;
using DCL.WebRequests.Analytics;
using ECS.StreamableLoading.Cache.Disk;
using ECS.StreamableLoading.Cache.InMemory;
using ECS.StreamableLoading.Common.Components;
using Global.AppArgs;
using Global.Dynamic.DebugSettings;
using Global.Dynamic.LaunchModes;
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
        private readonly ISplashScreen splashScreen;
        private readonly RealmLaunchSettings realmLaunchSettings;
        private readonly WebRequestsContainer webRequestsContainer;
        private readonly IDiskCache diskCache;
        private readonly World world;
        private readonly ObjectProxy<IProfileRepository> profileRepositoryProxy = new ();

        private URLDomain? startingRealm;
        private Vector2Int startingParcel;
        private DynamicWorldDependencies dynamicWorldDependencies;

        public bool EnableAnalytics { private get; init; }

        public Bootstrap(
            DebugSettings.DebugSettings debugSettings,
            IAppArgs appArgs,
            ISplashScreen splashScreen,
            RealmUrls realmUrls,
            RealmLaunchSettings realmLaunchSettings,
            WebRequestsContainer webRequestsContainer,
            IDiskCache diskCache,
            World world)
        {
            this.debugSettings = debugSettings;
            this.realmUrls = realmUrls;
            this.appArgs = appArgs;
            this.splashScreen = splashScreen;
            this.realmLaunchSettings = realmLaunchSettings;
            this.webRequestsContainer = webRequestsContainer;
            this.diskCache = diskCache;
            this.world = world;
        }

        public async UniTask PreInitializeSetupAsync(UIDocument cursorRoot,
            UIDocument debugUiRoot,
            CancellationToken token)
        {
            splashScreen.Show();

            cursorRoot.EnsureNotNull();

            Uri realm = await realmUrls.StartingRealmAsync(token);
            startingRealm = URLDomain.FromString(realm);

            // Hides the debug UI during the initial flow
            debugUiRoot.rootVisualElement.EnsureNotNull().style.display = DisplayStyle.None;

            // Initialize .NET logging ASAP since it might be used by another systems
            // Otherwise we might get exceptions in different platforms
            DotNetLoggingPlugin.Initialize();
        }

        public async UniTask<(StaticContainer?, bool)> LoadStaticContainerAsync(
            BootstrapContainer bootstrapContainer,
            PluginSettingsContainer globalPluginSettingsContainer,
            IDebugContainerBuilder debugContainerBuilder,
            Entity playerEntity,
            ISystemMemoryCap memoryCap,
            UIDocument sceneUIRoot,
            CancellationToken ct
        ) =>
            await StaticContainer.CreateAsync(
                bootstrapContainer.DecentralandUrlsSource,
                bootstrapContainer.AssetsProvisioner,
                bootstrapContainer.ReportHandlingSettings,
                debugContainerBuilder,
                webRequestsContainer,
                globalPluginSettingsContainer,
                bootstrapContainer.DiagnosticsContainer,
                bootstrapContainer.IdentityCache,
                bootstrapContainer.VerifiedEthereumApi,
                bootstrapContainer.LaunchMode,
                bootstrapContainer.UseRemoteAssetBundles,
                world,
                playerEntity,
                memoryCap,
                bootstrapContainer.WorldVolumeMacBus,
                EnableAnalytics,
                bootstrapContainer.Analytics,
                diskCache,
                sceneUIRoot,
                profileRepositoryProxy,
                ct
            );

        public async UniTask<(DynamicWorldContainer?, bool)> LoadDynamicWorldContainerAsync(
            BootstrapContainer bootstrapContainer,
            StaticContainer staticContainer,
            PluginSettingsContainer scenePluginSettingsContainer,
            DynamicSceneLoaderSettings settings,
            DynamicSettings dynamicSettings,
            UIDocument uiToolkitRoot,
            UIDocument scenesUIRoot,
            UIDocument cursorRoot,
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
                uiToolkitRoot,
                scenesUIRoot,
                cursorRoot,
                dynamicSettings,
                bootstrapContainer.Web3Authenticator,
                bootstrapContainer.IdentityCache,
                splashScreen,
                worldInfoTool
            );

            Uri defaultStartingRealm = await realmUrls.StartingRealmAsync(ct);
            Uri? localSceneDevelopmentRealm = await realmUrls.LocalSceneDevelopmentRealmAsync(ct);



            (DynamicWorldContainer? container, bool success) tuple = await DynamicWorldContainer.CreateAsync(
                bootstrapContainer,
                dynamicWorldDependencies,
                new DynamicWorldParams
                {
                    StaticLoadPositions = realmLaunchSettings.GetPredefinedParcels(),
                    Realms = settings.Realms,
                    DefaultStartingRealm = defaultStartingRealm,
                    StartParcel = new StartParcel(realmLaunchSettings.targetScene),
                    IsolateScenesCommunication = realmLaunchSettings.isolateSceneCommunication,
                    EnableLandscape = debugSettings.EnableLandscape,
                    EnableLOD = debugSettings.EnableLOD && realmLaunchSettings.CurrentMode is LaunchMode.Play,
                    EnableAnalytics = EnableAnalytics,
                    HybridSceneParams = realmLaunchSettings.CreateHybridSceneParams(),
                    LocalSceneDevelopmentRealm = localSceneDevelopmentRealm,
                    AppParameters = appArgs,
                },
                backgroundMusic,
                world,
                playerEntity,
                appArgs,
                coroutineRunner,
                dclVersion,
                realmUrls,
                ct);

            if (tuple.container != null)
                profileRepositoryProxy.SetObject(tuple.container.ProfileRepository);

            return tuple;
        }

        public async UniTask<bool> InitializePluginsAsync(StaticContainer staticContainer, DynamicWorldContainer dynamicWorldContainer,
            PluginSettingsContainer scenePluginSettingsContainer, PluginSettingsContainer globalPluginSettingsContainer,
            CancellationToken ct)
        {
            var anyFailure = false;

            await UniTask.WhenAll(staticContainer.ECSWorldPlugins.Select(gp => scenePluginSettingsContainer.InitializePluginAsync(gp, ct).ContinueWith(OnPluginInitialized)).EnsureNotNull());
            await UniTask.WhenAll(dynamicWorldContainer.GlobalPlugins.Select(gp => globalPluginSettingsContainer.InitializePluginAsync(gp, ct).ContinueWith(OnPluginInitialized)).EnsureNotNull());

            void OnPluginInitialized<TPluginInterface>((TPluginInterface plugin, bool success) result) where TPluginInterface: IDCLPlugin
            {
                if (!result.success)
                    anyFailure = true;
            }

            return anyFailure;
        }

        public async UniTask InitializeFeatureFlagsAsync(IWeb3Identity? identity, IDecentralandUrlsSource decentralandUrlsSource, StaticContainer staticContainer, CancellationToken ct)
        {
            try { await staticContainer.FeatureFlagsProvider.InitializeAsync(decentralandUrlsSource, identity?.Address, appArgs, ct); }
            catch (Exception e) when (e is not OperationCanceledException) { ReportHub.LogException(e, new ReportData(ReportCategory.FEATURE_FLAGS)); }
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
                webJsSources
            );

            GlobalWorld globalWorld = dynamicWorldContainer.GlobalWorldFactory.Create(
                sceneSharedContainer.SceneFactory, playerEntity);

            dynamicWorldContainer.RealmController.GlobalWorld = globalWorld;
            staticContainer.PortableExperiencesController.GlobalWorld = globalWorld;

            staticContainer.DebugContainerBuilder.BuildWithFlex(debugUiRoot);
            staticContainer.DebugContainerBuilder.IsVisible = appArgs.HasDebugFlag();

            return globalWorld;
        }

        public void InitializePlayerEntity(StaticContainer staticContainer, Entity playerEntity)
        {
            staticContainer.CharacterContainer.InitializePlayerEntity(world, playerEntity);
        }

        public async UniTask LoadStartingRealmAsync(DynamicWorldContainer dynamicWorldContainer, CancellationToken ct)
        {
            if (startingRealm.HasValue == false)
                throw new InvalidOperationException("Starting realm is not set");

            await dynamicWorldContainer.RealmController.SetRealmAsync(startingRealm.Value, ct);
        }

        public void ApplyFeatureFlagConfigs(FeatureFlagsConfiguration featureFlagsConfigurationCache)
        {
            realmLaunchSettings.CheckStartParcelFeatureFlagOverride(appArgs, featureFlagsConfigurationCache);
            webRequestsContainer.SetKTXEnabled(featureFlagsConfigurationCache.IsEnabled(FeatureFlagsStrings.KTX2_CONVERSION));
        }

        public async UniTask UserInitializationAsync(DynamicWorldContainer dynamicWorldContainer,
            GlobalWorld globalWorld, Entity playerEntity, CancellationToken ct)
        {
            splashScreen.Show();

            await dynamicWorldContainer.UserInAppInAppInitializationFlow.ExecuteAsync(
                new UserInAppInitializationFlowParameters
                (
                    showAuthentication: debugSettings.ShowAuthentication,
                    showLoading: debugSettings.ShowLoading,
                    IUserInAppInitializationFlow.LoadSource.StartUp,
                    world: globalWorld.EcsWorld,
                    playerEntity: playerEntity
                ), ct);

            OpenDefaultUI(dynamicWorldContainer.MvcManager, ct);
            splashScreen.Hide();
        }

        private static void OpenDefaultUI(IMVCManager mvcManager, CancellationToken ct)
        {
            mvcManager.ShowAsync(NewNotificationController.IssueCommand(), ct).Forget();
            mvcManager.ShowAsync(MainUIController.IssueCommand(), ct).Forget();
        }
    }
}
