using Arch.Core;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Audio;
using DCL.CommandLine;
using DCL.DebugUtilities;
using DCL.Diagnostics;
using DCL.EmotesWheel;
using DCL.FeatureFlags;
using DCL.Input;
using DCL.Input.Component;
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
        private readonly IDebugSettings debugSettings;
        private readonly ICommandLineArgs commandLineArgs;

        private URLDomain startingRealm = URLDomain.FromString(IRealmNavigator.GENESIS_URL);
        private Vector2Int startingParcel;
        private DynamicWorldDependencies dynamicWorldDependencies;
        private Dictionary<string, string> appParameters = new ();

        public bool EnableAnalytics { private get; init; }

        public Bootstrap(IDebugSettings debugSettings, ICommandLineArgs commandLineArgs)
        {
            this.debugSettings = debugSettings;
            this.commandLineArgs = commandLineArgs;
        }

        public void PreInitializeSetup(RealmLaunchSettings launchSettings, UIDocument cursorRoot, UIDocument debugUiRoot,
            ISplashScreen splashScreen, CancellationToken _)
        {
            splashScreen.ShowSplash();
            cursorRoot.EnsureNotNull();

            startingRealm = URLDomain.FromString(launchSettings.GetStartingRealm());
            startingParcel = launchSettings.TargetScene;

            // Hides the debug UI during the initial flow
            debugUiRoot.rootVisualElement.EnsureNotNull().style.display = DisplayStyle.None;

            // Initialize .NET logging ASAP since it might be used by another systems
            // Otherwise we might get exceptions in different platforms
            DotNetLoggingPlugin.Initialize();
        }

        public async UniTask<(StaticContainer?, bool)> LoadStaticContainerAsync(BootstrapContainer bootstrapContainer, PluginSettingsContainer globalPluginSettingsContainer, DebugViewsCatalog debugViewsCatalog, CancellationToken ct) =>
            await StaticContainer.CreateAsync(bootstrapContainer.DecentralandUrlsSource, bootstrapContainer.AssetsProvisioner, bootstrapContainer.ReportHandlingSettings, commandLineArgs, debugViewsCatalog, globalPluginSettingsContainer,
                bootstrapContainer.IdentityCache, bootstrapContainer.VerifiedEthereumApi, ct);

        public async UniTask<(DynamicWorldContainer?, bool)> LoadDynamicWorldContainerAsync(
            BootstrapContainer bootstrapContainer,
            StaticContainer staticContainer,
            PluginSettingsContainer scenePluginSettingsContainer,
            DynamicSceneLoaderSettings settings,
            DynamicSettings dynamicSettings,
            RealmLaunchSettings launchSettings,
            UIDocument uiToolkitRoot,
            UIDocument cursorRoot,
            ISplashScreen splashScreen,
            AudioClipConfig backgroundMusic,
            WorldInfoTool worldInfoTool,
            CancellationToken ct
        )
        {
            dynamicWorldDependencies = new DynamicWorldDependencies
            (
                staticContainer.DebugContainerBuilder,
                commandLineArgs,
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
                    StaticLoadPositions = launchSettings.GetPredefinedParcels(),
                    Realms = settings.Realms,
                    StartParcel = startingParcel,
                    EnableLandscape = debugSettings.EnableLandscape && !bootstrapContainer.LocalSceneDevelopment,
                    EnableLOD = debugSettings.EnableLOD && !bootstrapContainer.LocalSceneDevelopment,
                    EnableAnalytics = EnableAnalytics, HybridSceneParams = launchSettings.CreateHybridSceneParams(startingParcel),
                    LocalSceneDevelopmentRealm = bootstrapContainer.LocalSceneDevelopment ? launchSettings.GetStartingRealm() : string.Empty,
                    AppParameters = appParameters,
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
            try { await staticContainer.FeatureFlagsProvider.InitializeAsync(decentralandUrlsSource, identity?.Address, appParameters, ct); }
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
                !bootstrapContainer.LocalSceneDevelopment
            );

            (globalWorld, playerEntity) = dynamicWorldContainer.GlobalWorldFactory.Create(sceneSharedContainer.SceneFactory);
            dynamicWorldContainer.RealmController.GlobalWorld = globalWorld;

            staticContainer.DebugContainerBuilder.BuildWithFlex(debugUiRoot);
            staticContainer.DebugContainerBuilder.IsVisible = commandLineArgs.HasDebugFlag();

            return (globalWorld, playerEntity);
        }

        public async UniTask LoadStartingRealmAsync(DynamicWorldContainer dynamicWorldContainer, CancellationToken ct)
        {
            await dynamicWorldContainer.RealmController.SetRealmAsync(startingRealm, ct);
        }

        public async UniTask UserInitializationAsync(DynamicWorldContainer dynamicWorldContainer,
            GlobalWorld globalWorld, Entity playerEntity, ISplashScreen splashScreen, CancellationToken ct)
        {
            splashScreen.ShowSplash();

            await dynamicWorldContainer.UserInAppInAppInitializationFlow.ExecuteAsync(
                debugSettings.ShowAuthentication,
                debugSettings.ShowLoading,
                false,
                globalWorld.EcsWorld,
                playerEntity,
                ct
            );

            OpenDefaultUI(dynamicWorldContainer.MvcManager, ct);
            splashScreen.HideSplash();
        }

        private static void OpenDefaultUI(IMVCManager mvcManager, CancellationToken ct)
        {
            mvcManager.ShowAsync(NewNotificationController.IssueCommand(), ct).Forget();
            mvcManager.ShowAsync(MainUIController.IssueCommand(), ct).Forget();
        }
    }
}
