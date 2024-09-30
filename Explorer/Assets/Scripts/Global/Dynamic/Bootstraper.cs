using Arch.Core;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Audio;
using DCL.DebugUtilities;
using DCL.Diagnostics;
using DCL.FeatureFlags;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Multiplayer.Movement.Systems;
using DCL.Notifications.NewNotification;
using DCL.PerformanceAndDiagnostics.DotNetLogging;
using DCL.PluginSystem;
using DCL.PluginSystem.Global;
using DCL.SceneLoadingScreens.SplashScreen;
using DCL.UI.MainUI;
using DCL.Utilities.Extensions;
using DCL.Web3.Identities;
using Global.AppArgs;
using Global.Dynamic.DebugSettings;
using MVC;
using PortableExperiences.Controller;
using SceneRunner.Debugging;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.UIElements;

namespace Global.Dynamic
{
    public class Bootstrap : IBootstrap
    {
        private readonly IDebugSettings debugSettings;
        private readonly IDecentralandUrlsSource decentralandUrlsSource;
        private readonly IAppArgs appArgs;
        private readonly RealmLaunchSettings realmLaunchSettings;
        private readonly World world;

        private URLDomain? startingRealm;
        private Vector2Int startingParcel;
        private DynamicWorldDependencies dynamicWorldDependencies;

        public bool EnableAnalytics { private get; init; }

        public Bootstrap(IDebugSettings debugSettings,
            IAppArgs appArgs,
            IDecentralandUrlsSource decentralandUrlsSource,
            RealmLaunchSettings realmLaunchSettings,
            World world)
        {
            this.debugSettings = debugSettings;
            this.decentralandUrlsSource = decentralandUrlsSource;
            this.appArgs = appArgs;
            this.realmLaunchSettings = realmLaunchSettings;
            this.world = world;
        }

        public void PreInitializeSetup(UIDocument cursorRoot,
            UIDocument debugUiRoot,
            ISplashScreen splashScreen,
            CancellationToken _)
        {
            splashScreen.Show();
            cursorRoot.EnsureNotNull();

            startingRealm = URLDomain.FromString(realmLaunchSettings.GetStartingRealm(decentralandUrlsSource));
            startingParcel = realmLaunchSettings.TargetScene;

            // Hides the debug UI during the initial flow
            debugUiRoot.rootVisualElement.EnsureNotNull().style.display = DisplayStyle.None;

            // Initialize .NET logging ASAP since it might be used by another systems
            // Otherwise we might get exceptions in different platforms
            DotNetLoggingPlugin.Initialize();
        }

        public async UniTask<(StaticContainer?, bool)> LoadStaticContainerAsync(BootstrapContainer bootstrapContainer, PluginSettingsContainer globalPluginSettingsContainer, DebugViewsCatalog debugViewsCatalog, Entity playerEntity, CancellationToken ct) =>
            await StaticContainer.CreateAsync(bootstrapContainer.DecentralandUrlsSource, bootstrapContainer.AssetsProvisioner, bootstrapContainer.ReportHandlingSettings, appArgs, debugViewsCatalog, globalPluginSettingsContainer,
                bootstrapContainer.DiagnosticsContainer, bootstrapContainer.IdentityCache, bootstrapContainer.VerifiedEthereumApi, bootstrapContainer.LocalSceneDevelopment, bootstrapContainer.UseRemoteAssetBundles, world, playerEntity, ct);

        public async UniTask<(DynamicWorldContainer?, bool)> LoadDynamicWorldContainerAsync(BootstrapContainer bootstrapContainer,
            StaticContainer staticContainer,
            PluginSettingsContainer scenePluginSettingsContainer,
            DynamicSceneLoaderSettings settings,
            DynamicSettings dynamicSettings,
            UIDocument uiToolkitRoot,
            UIDocument cursorRoot,
            ISplashScreen splashScreen,
            AudioClipConfig backgroundMusic,
            WorldInfoTool worldInfoTool,
            Entity playerEntity,
            IAppArgs appArgs,
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
                cursorRoot,
                dynamicSettings,
                bootstrapContainer.Web3Authenticator,
                bootstrapContainer.IdentityCache,
                splashScreen,
                worldInfoTool
            );

            return await DynamicWorldContainer.CreateAsync(
                bootstrapContainer,
                dynamicWorldDependencies,
                new DynamicWorldParams
                {
                    StaticLoadPositions = realmLaunchSettings.GetPredefinedParcels(),
                    Realms = settings.Realms,
                    StartParcel = startingParcel,
                    IsolateScenesCommunication = realmLaunchSettings.isolateSceneCommunication,
                    EnableLandscape = debugSettings.EnableLandscape && !realmLaunchSettings.IsLocalSceneDevelopmentRealm,
                    EnableLOD = debugSettings.EnableLOD && !realmLaunchSettings.IsLocalSceneDevelopmentRealm,
                    EnableAnalytics = EnableAnalytics,
                    HybridSceneParams = realmLaunchSettings.CreateHybridSceneParams(startingParcel),
                    LocalSceneDevelopmentRealm = realmLaunchSettings.GetLocalSceneDevelopmentRealm(decentralandUrlsSource) ?? string.Empty,
                    AppParameters = appArgs,
                },
                backgroundMusic,
                staticContainer.PortableExperiencesController,
                world,
                playerEntity,
                appArgs,
                ct);
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
            SceneSharedContainer sceneSharedContainer = SceneSharedContainer.Create(
                in staticContainer,
                bootstrapContainer.DecentralandUrlsSource,
                bootstrapContainer.IdentityCache,
                staticContainer.WebRequestsContainer.WebRequestController,
                dynamicWorldContainer.RealmController.RealmData,
                dynamicWorldContainer.ProfileRepository,
                dynamicWorldContainer.RoomHub,
                dynamicWorldContainer.MvcManager,
                dynamicWorldContainer.MessagePipesHub,
                !realmLaunchSettings.IsLocalSceneDevelopmentRealm
            );

            GlobalWorld globalWorld = dynamicWorldContainer.GlobalWorldFactory.Create(sceneSharedContainer.SceneFactory,
                sceneSharedContainer.V8ActiveEngines, playerEntity);
            dynamicWorldContainer.RealmController.GlobalWorld = globalWorld;

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

        public async UniTask UserInitializationAsync(DynamicWorldContainer dynamicWorldContainer,
            GlobalWorld globalWorld, Entity playerEntity, ISplashScreen splashScreen, CancellationToken ct)
        {
            splashScreen.Show();

            await dynamicWorldContainer.UserInAppInAppInitializationFlow.ExecuteAsync(
                debugSettings.ShowAuthentication,
                debugSettings.ShowLoading,
                false,
                globalWorld.EcsWorld,
                playerEntity,
                ct
            );

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
