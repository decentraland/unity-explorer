using Arch.Core;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Audio;
using DCL.Browser;
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
using DCL.Web3.Authenticators;
using DCL.Web3.Identities;
using ECS.SceneLifeCycle.Realm;
using MVC;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.UIElements;

namespace Global.Dynamic
{
    public class Bootstrap : IBootstrap
    {
        private readonly bool showSplash = true;
        private readonly bool showAuthentication = true;
        private readonly bool showLoading = true;
        private readonly bool enableLOD = true;
        private readonly bool enableLandscape = true;

        private DebugUtilitiesContainer debugUtilitiesContainer;

        private string startingRealm = IRealmNavigator.GENESIS_URL;
        private Vector2Int startingParcel;

        private DappWeb3Authenticator web3VerifiedAuthenticator;
        private ProxyVerifiedWeb3Authenticator web3Authenticator;

        public LogWeb3IdentityCache IdentityCache { get; private set; }
        public DynamicWorldDependencies DynamicWorldDependencies { get; private set; }

        public Bootstrap(bool showSplash, bool showAuthentication, bool showLoading, bool enableLOD, bool enableLandscape)
        {
            // To avoid configuration issues, force full flow on build (Debug.isDebugBuild is always true in Editor)
            if (!Debug.isDebugBuild) return;

            this.showSplash = showSplash;
            this.showAuthentication = showAuthentication;
            this.showLoading = showLoading;
            this.enableLOD = enableLOD;
            this.enableLandscape = enableLandscape;
        }

        public void Dispose()
        {
            web3Authenticator.Dispose();
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

        public async UniTask<(StaticContainer?, bool)> LoadStaticContainerAsync(PluginSettingsContainer globalPluginSettingsContainer,
            DynamicSceneLoaderSettings settings, IAssetsProvisioner assetsProvisioner, CancellationToken ct)
        {
            IdentityCache = new LogWeb3IdentityCache(
                new ProxyIdentityCache(
                    new MemoryWeb3IdentityCache(),
                    new PlayerPrefsIdentityProvider(
                        new PlayerPrefsIdentityProvider.DecentralandIdentityWithNethereumAccountJsonSerializer()
                    )
                )
            );

            web3VerifiedAuthenticator = new DappWeb3Authenticator(new UnityAppWebBrowser(),
                serverUrl: GetAuthUrl(settings.AuthWebSocketUrl, settings.AuthWebSocketUrlDev),
                signatureUrl: GetAuthUrl(settings.AuthSignatureUrl, settings.AuthSignatureUrlDev),
                IdentityCache,
                new HashSet<string>(settings.Web3WhitelistMethods));

            web3Authenticator = new ProxyVerifiedWeb3Authenticator(web3VerifiedAuthenticator, IdentityCache);

            return await StaticContainer.CreateAsync(assetsProvisioner, debugUtilitiesContainer.Builder, globalPluginSettingsContainer, IdentityCache, web3VerifiedAuthenticator, ct);

            // Allow devUrl only in DebugBuilds (Debug.isDebugBuild is always true in Editor)
            string GetAuthUrl(string releaseUrl, string devUrl) =>
                Application.isEditor || !Debug.isDebugBuild ? releaseUrl : devUrl;
        }

        public async UniTask<(DynamicWorldContainer?, bool)> LoadDynamicWorldContainerAsync(StaticContainer staticContainer,
            PluginSettingsContainer scenePluginSettingsContainer, DynamicSceneLoaderSettings settings, DynamicSettings dynamicSettings, RealmLaunchSettings launchSettings,
            UIDocument uiToolkitRoot, UIDocument cursorRoot, Animator splashScreenAnimation, AudioClipConfig backgroundMusic, CancellationToken ct)
        {
            DynamicWorldDependencies = new DynamicWorldDependencies
            {
                DebugContainerBuilder = debugUtilitiesContainer.Builder,
                StaticContainer = staticContainer!,
                SettingsContainer = scenePluginSettingsContainer,
                RootUIDocument = uiToolkitRoot,
                CursorUIDocument = cursorRoot,
                DynamicSettings = dynamicSettings,
                Web3Authenticator = web3Authenticator,
                Web3IdentityCache = IdentityCache,
                SplashAnimator = splashScreenAnimation,
            };

            return await DynamicWorldContainer.CreateAsync(
                DynamicWorldDependencies,
                new DynamicWorldParams
                {
                    StaticLoadPositions = launchSettings.GetPredefinedParcels(),
                    Realms = settings.Realms,
                    StartParcel = startingParcel,
                    EnableLandscape = enableLandscape,
                    EnableLOD = enableLOD,
                    HybridSceneParams = launchSettings.CreateHybridSceneParams(),
                },
                backgroundMusic,
                ct
            );
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

        public async UniTask InitializeFeatureFlagsAsync(StaticContainer staticContainer, CancellationToken ct)
        {
            try
            {
                await staticContainer!.FeatureFlagsProvider.InitializeAsync(IdentityCache!.Identity?.Address, ct);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                ReportHub.LogException(e, new ReportData(ReportCategory.FEATURE_FLAGS));
            }
        }

        public (GlobalWorld, Entity) CreateGlobalWorldAndPlayer(StaticContainer staticContainer, DynamicWorldContainer dynamicWorldContainer, UIDocument debugUiRoot)
        {
            Entity playerEntity;
            GlobalWorld globalWorld;

            var sceneSharedContainer = SceneSharedContainer.Create(
                in staticContainer!,
                dynamicWorldContainer!.MvcManager,
                IdentityCache,
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

        public async UniTask LoadStartingRealmAndUserInitializationAsync(DynamicWorldContainer dynamicWorldContainer,
            GlobalWorld? globalWorld, Entity playerEntity, Animator splashScreenAnimation, GameObject splashRoot, CancellationToken ct)
        {
            await dynamicWorldContainer.RealmController.SetRealmAsync(URLDomain.FromString(startingRealm), ct);

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
