using Arch.Core;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Audio;
using DCL.DebugUtilities;
using DCL.Diagnostics;
using DCL.EmotesWheel;
using DCL.FeatureFlags;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Notifications.NewNotification;
using DCL.PerformanceAndDiagnostics.DotNetLogging;
using DCL.PluginSystem;
using DCL.PluginSystem.Global;
using DCL.UI.MainUI;
using DCL.SceneLoadingScreens.SplashScreen;
using DCL.Utilities.Extensions;
using DCL.Web3.Identities;
using ECS.SceneLifeCycle.Realm;
using MVC;
using SceneRunner.Debugging;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.UIElements;

namespace Global.Dynamic
{
    public class Bootstrap : IBootstrap
    {
        private readonly bool showSplash;
        private readonly bool showAuthentication;
        private readonly bool showLoading;
        private readonly bool enableLOD;
        private readonly bool enableLandscape;
        private readonly ApplicationParametersParser applicationParametersParser;
        private readonly RealmLaunchSettings realmLaunchSettings;
        private readonly IDebugSettings debugSettings;

        private URLDomain startingRealm = URLDomain.FromString(IRealmNavigator.GENESIS_URL);
        private Vector2Int startingParcel;
        private DynamicWorldDependencies dynamicWorldDependencies;

        public bool EnableAnalytics { private get; init; }

        public Bootstrap(IDebugSettings debugSettings,
            ApplicationParametersParser applicationParametersParser,
            RealmLaunchSettings realmLaunchSettings)
        {
            this.debugSettings = debugSettings;
            this.applicationParametersParser = applicationParametersParser;
            this.realmLaunchSettings = realmLaunchSettings;
        }

        public void PreInitializeSetup(UIDocument cursorRoot, UIDocument debugUiRoot,
            ISplashScreen splashScreen, CancellationToken _)
        {
            splashScreen.Show();
            cursorRoot.EnsureNotNull();

            startingRealm = URLDomain.FromString(realmLaunchSettings.GetStartingRealm());
            string settingsRealm = realmLaunchSettings.GetStartingRealm();

            startingRealm = URLDomain.FromString(settingsRealm);
            startingParcel = realmLaunchSettings.TargetScene;

            // Hides the debug UI during the initial flow
            debugUiRoot.rootVisualElement.EnsureNotNull().style.display = DisplayStyle.None;

            // Initialize .NET logging ASAP since it might be used by another systems
            // Otherwise we might get exceptions in different platforms
            DotNetLoggingPlugin.Initialize();
        }

        public async UniTask<(StaticContainer?, bool)> LoadStaticContainerAsync(BootstrapContainer bootstrapContainer, PluginSettingsContainer globalPluginSettingsContainer, DebugViewsCatalog debugViewsCatalog, CancellationToken ct) =>
            await StaticContainer.CreateAsync(bootstrapContainer.DecentralandUrlsSource, bootstrapContainer.AssetsProvisioner, bootstrapContainer.ReportHandlingSettings, debugViewsCatalog, globalPluginSettingsContainer,
                bootstrapContainer.IdentityCache, bootstrapContainer.VerifiedEthereumApi, ct);

        public async UniTask<(DynamicWorldContainer?, bool)> LoadDynamicWorldContainerAsync(BootstrapContainer bootstrapContainer, StaticContainer staticContainer,
            PluginSettingsContainer scenePluginSettingsContainer, DynamicSceneLoaderSettings settings, DynamicSettings dynamicSettings, RealmLaunchSettings launchSettings,
            UIDocument uiToolkitRoot, UIDocument cursorRoot, ISplashScreen splashScreen, AudioClipConfig backgroundMusic, WorldInfoTool worldInfoTool,
            CancellationToken ct)
        {
            dynamicWorldDependencies = new DynamicWorldDependencies
            {
                DebugContainerBuilder = staticContainer.DebugContainerBuilder,
                AssetsProvisioner = bootstrapContainer.AssetsProvisioner,
                StaticContainer = staticContainer,
                SettingsContainer = scenePluginSettingsContainer,
                DynamicSettings = dynamicSettings,
                Web3Authenticator = bootstrapContainer.Web3Authenticator,
                Web3IdentityCache = bootstrapContainer.IdentityCache,
                RootUIDocument = uiToolkitRoot,
                CursorUIDocument = cursorRoot,
                SplashScreen = splashScreen,
                WorldInfoTool = worldInfoTool,
            };

            return await DynamicWorldContainer.CreateAsync(
                bootstrapContainer,
                dynamicWorldDependencies,
                new DynamicWorldParams
                {
                    StaticLoadPositions = launchSettings.GetPredefinedParcels(),
                    Realms = settings.Realms,
                    StartParcel = startingParcel,
                    EnableLandscape = debugSettings.EnableLandscape && !bootstrapContainer.LocalSceneDevelopment,
                    EnableLOD = debugSettings.EnableLOD && !bootstrapContainer.LocalSceneDevelopment,
                    EnableAnalytics = EnableAnalytics, HybridSceneParams = launchSettings.CreateHybridSceneParams(startingParcel),
                    LocalSceneDevelopmentRealm = launchSettings.GetLocalSceneDevelopmentRealm(),
                    AppParameters = applicationParametersParser.AppParameters,
                },
                backgroundMusic,
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
            try { await staticContainer.FeatureFlagsProvider.InitializeAsync(decentralandUrlsSource, identity?.Address, applicationParametersParser.AppParameters, ct); }
            catch (Exception e) when (e is not OperationCanceledException) { ReportHub.LogException(e, new ReportData(ReportCategory.FEATURE_FLAGS)); }
        }

        public (GlobalWorld, Entity) CreateGlobalWorldAndPlayer(
            BootstrapContainer bootstrapContainer,
            StaticContainer staticContainer,
            DynamicWorldContainer dynamicWorldContainer,
            UIDocument debugUiRoot
        )
        {
            Entity playerEntity;
            GlobalWorld globalWorld;

            var sceneSharedContainer = SceneSharedContainer.Create(
                in staticContainer,
                bootstrapContainer.DecentralandUrlsSource,
                dynamicWorldContainer.MvcManager,
                bootstrapContainer.IdentityCache,
                dynamicWorldContainer.ProfileRepository,
                staticContainer.WebRequestsContainer.WebRequestController,
                dynamicWorldContainer.RoomHub,
                dynamicWorldContainer.RealmController.RealmData,
                dynamicWorldContainer.MessagePipesHub,
                !realmLaunchSettings.IsLocalSceneDevelopmentRealm
            );

            (globalWorld, playerEntity) = dynamicWorldContainer.GlobalWorldFactory.Create(sceneSharedContainer.SceneFactory);
            dynamicWorldContainer.RealmController.GlobalWorld = globalWorld;

            staticContainer.DebugContainerBuilder.BuildWithFlex(debugUiRoot);

            return (globalWorld, playerEntity);
        }

        public async UniTask LoadStartingRealmAsync(DynamicWorldContainer dynamicWorldContainer, CancellationToken ct)
        {
            await dynamicWorldContainer.RealmController.SetRealmAsync(startingRealm, ct);
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

            splashScreen.Hide();
            OpenDefaultUI(dynamicWorldContainer.MvcManager, ct);
        }

        private static void OpenDefaultUI(IMVCManager mvcManager, CancellationToken ct)
        {
            // TODO: all of these UIs should be part of a single canvas. We cannot make a proper layout by having them separately
            mvcManager.ShowAsync(MainUIController.IssueCommand(), ct).Forget();
            mvcManager.ShowAsync(NewNotificationController.IssueCommand(), ct).Forget();
            mvcManager.ShowAsync(PersistentEmoteWheelOpenerController.IssueCommand(), ct).Forget();
        }
    }
}
