using Arch.Core;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Audio;
using DCL.Chat;
using DCL.DebugUtilities;
using DCL.Diagnostics;
using DCL.EmotesWheel;
using DCL.ExplorePanel;
using DCL.FeatureFlags;
using DCL.Minimap;
using DCL.PerformanceAndDiagnostics.DotNetLogging;
using DCL.PluginSystem;
using DCL.PluginSystem.Global;
using DCL.Utilities.Extensions;
using DCL.Web3.Identities;
using ECS.SceneLifeCycle.Realm;
using MVC;
using System;
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
        public bool EnableAnalytics { private get; set; } = false;

        private DebugUtilitiesContainer debugUtilitiesContainer;

        private string startingRealm = IRealmNavigator.GENESIS_URL;
        private Vector2Int startingParcel;

        public DynamicWorldDependencies DynamicWorldDependencies { get; private set; }

        public Bootstrap(DebugSettings debugSettings)
        {
            showSplash = debugSettings.showSplash;
            showAuthentication = debugSettings.showAuthentication;
            showLoading = debugSettings.showLoading;
            enableLOD = debugSettings.enableLOD;
            enableLandscape = debugSettings.enableLandscape;
        }

        public void Dispose()
        {
        }

        public UniTask PreInitializeSetup(RealmLaunchSettings launchSettings, UIDocument cursorRoot, UIDocument debugUiRoot,
            GameObject splashRoot, DebugViewsCatalog debugViewsCatalog, CancellationToken _)
        {
            splashRoot.SetActive(showSplash);
            cursorRoot.EnsureNotNull();

            startingRealm = launchSettings.GetStartingRealm();
            startingParcel = launchSettings.TargetScene;

            // Hides the debug UI during the initial flow
            debugUiRoot.rootVisualElement.EnsureNotNull().style.display = DisplayStyle.None;

            // Initialize .NET logging ASAP since it might be used by another systems
            // Otherwise we might get exceptions in different platforms
            DotNetLoggingPlugin.Initialize();

            debugUtilitiesContainer = DebugUtilitiesContainer.Create(debugViewsCatalog);

            return UniTask.CompletedTask;
        }

        public async UniTask<(StaticContainer?, bool)> LoadStaticContainerAsync(BootstrapContainer bootstrapContainer, PluginSettingsContainer globalPluginSettingsContainer, CancellationToken ct) =>
            await StaticContainer.CreateAsync(bootstrapContainer.AssetsProvisioner, debugUtilitiesContainer.Builder, globalPluginSettingsContainer,
                bootstrapContainer.IdentityCache, bootstrapContainer.Web3VerifiedAuthenticator, ct);

        public async UniTask<(DynamicWorldContainer?, bool)> LoadDynamicWorldContainerAsync(BootstrapContainer bootstrapContainer, StaticContainer staticContainer,
            PluginSettingsContainer scenePluginSettingsContainer, DynamicSceneLoaderSettings settings, DynamicSettings dynamicSettings, RealmLaunchSettings launchSettings,
            UIDocument uiToolkitRoot, UIDocument cursorRoot, Animator splashScreenAnimation, AudioClipConfig backgroundMusic, CancellationToken ct)
        {
            DynamicWorldDependencies = new DynamicWorldDependencies
            {
                DebugContainerBuilder = debugUtilitiesContainer.Builder,
                AssetsProvisioner = bootstrapContainer.AssetsProvisioner,
                StaticContainer = staticContainer,
                SettingsContainer = scenePluginSettingsContainer,
                DynamicSettings = dynamicSettings,
                Web3Authenticator = bootstrapContainer.Web3Authenticator,
                Web3IdentityCache = bootstrapContainer.IdentityCache,
                RootUIDocument = uiToolkitRoot,
                CursorUIDocument = cursorRoot,
                SplashAnimator = splashScreenAnimation,
            };

            return await DynamicWorldContainer.CreateAsync(
                bootstrapContainer,
                DynamicWorldDependencies,
                new DynamicWorldParams
                {
                    StaticLoadPositions = launchSettings.GetPredefinedParcels(),
                    Realms = settings.Realms,
                    StartParcel = startingParcel,
                    EnableLandscape = enableLandscape,
                    EnableLOD = enableLOD,
                    EnableAnalytics = EnableAnalytics,
                    HybridSceneParams = launchSettings.CreateHybridSceneParams(),
                },
                backgroundMusic,
                ct);
        }

        public async UniTask<bool> InitializePluginsAsync(StaticContainer staticContainer, DynamicWorldContainer dynamicWorldContainer,
            PluginSettingsContainer scenePluginSettingsContainer, PluginSettingsContainer globalPluginSettingsContainer,
            CancellationToken ct)
        {
            var anyFailure = false;

            await UniTask.WhenAll(staticContainer!.ECSWorldPlugins.Select(gp => scenePluginSettingsContainer.InitializePluginAsync(gp, ct).ContinueWith(OnPluginInitialized)).EnsureNotNull());
            await UniTask.WhenAll(dynamicWorldContainer!.GlobalPlugins.Select(gp => globalPluginSettingsContainer.InitializePluginAsync(gp, ct).ContinueWith(OnPluginInitialized)).EnsureNotNull());

            void OnPluginInitialized<TPluginInterface>((TPluginInterface plugin, bool success) result) where TPluginInterface: IDCLPlugin
            {
                if (!result.success)
                    anyFailure = true;
            }

            return anyFailure;
        }

        public async UniTask InitializeFeatureFlagsAsync(IWeb3Identity identity, StaticContainer staticContainer, CancellationToken ct)
        {
            try { await staticContainer!.FeatureFlagsProvider.InitializeAsync(identity?.Address, ct); }
            catch (Exception e) when (e is not OperationCanceledException) { ReportHub.LogException(e, new ReportData(ReportCategory.FEATURE_FLAGS)); }
        }

        public (GlobalWorld, Entity) CreateGlobalWorldAndPlayer(BootstrapContainer bootstrapContainer, StaticContainer staticContainer, DynamicWorldContainer dynamicWorldContainer,
            UIDocument debugUiRoot)
        {
            Entity playerEntity;
            GlobalWorld globalWorld;

            var sceneSharedContainer = SceneSharedContainer.Create(
                in staticContainer!,
                dynamicWorldContainer!.MvcManager,
                bootstrapContainer.IdentityCache,
                dynamicWorldContainer.ProfileRepository,
                staticContainer!.WebRequestsContainer.WebRequestController,
                dynamicWorldContainer!.RoomHub,
                dynamicWorldContainer.RealmController.GetRealm(),
                dynamicWorldContainer.MessagePipesHub
            );

            (globalWorld, playerEntity) = dynamicWorldContainer!.GlobalWorldFactory.Create(sceneSharedContainer!.SceneFactory);
            dynamicWorldContainer.RealmController.GlobalWorld = globalWorld;

            debugUtilitiesContainer.Builder.BuildWithFlex(debugUiRoot);

            return (globalWorld, playerEntity);
        }

        public async UniTask LoadStartingRealmAsync(DynamicWorldContainer dynamicWorldContainer, CancellationToken ct)
        {
            await dynamicWorldContainer.RealmController.SetRealmAsync(URLDomain.FromString(startingRealm), ct);
        }

        public async UniTask UserInitializationAsync(DynamicWorldContainer dynamicWorldContainer,
            GlobalWorld? globalWorld, Entity playerEntity, Animator splashScreenAnimation, GameObject splashRoot, CancellationToken ct)
        {
            if (showSplash)
                await UniTask.WaitUntil(() => splashScreenAnimation.GetCurrentAnimatorStateInfo(0).normalizedTime > 1, cancellationToken: ct);

            splashScreenAnimation.transform.SetSiblingIndex(1);

            await dynamicWorldContainer!.UserInAppInitializationFlow.ExecuteAsync(showAuthentication, showLoading,
                globalWorld!.EcsWorld, playerEntity, ct);

            splashRoot.SetActive(false);
            OpenDefaultUI(dynamicWorldContainer.MvcManager, ct);
        }

        private static void OpenDefaultUI(IMVCManager mvcManager, CancellationToken ct)
        {
            // TODO: all of these UIs should be part of a single canvas. We cannot make a proper layout by having them separately
            mvcManager.ShowAsync(MinimapController.IssueCommand(), ct).Forget();
            mvcManager.ShowAsync(PersistentExplorePanelOpenerController.IssueCommand(new EmptyParameter()), ct).Forget();
            mvcManager.ShowAsync(ChatController.IssueCommand(), ct).Forget();
            mvcManager.ShowAsync(PersistentEmoteWheelOpenerController.IssueCommand(), ct).Forget();
        }
    }
}
