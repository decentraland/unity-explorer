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
using DCL.Notification.NewNotification;
using DCL.PerformanceAndDiagnostics.DotNetLogging;
using DCL.PluginSystem;
using DCL.PluginSystem.Global;
using DCL.Utilities.Extensions;
using DCL.Web3.Identities;
using ECS.SceneLifeCycle.Realm;
using MVC;
using SceneRunner.Debugging;
using System;
using System.Collections.Specialized;
using System.Threading;
using UnityEngine;
using UnityEngine.UIElements;
using System.Text.RegularExpressions;
using System.Web;

namespace Global.Dynamic
{
    public class Bootstrap : IBootstrap
    {
        private readonly bool showSplash;
        private readonly bool showAuthentication;
        private readonly bool showLoading;
        private readonly bool enableLOD;
        private readonly bool enableLandscape;

        public bool EnableAnalytics { private get; init; }

        private URLDomain startingRealm = URLDomain.FromString(IRealmNavigator.GENESIS_URL);
        private Vector2Int startingParcel;
        private bool localSceneDevelopment;
        private DynamicWorldDependencies dynamicWorldDependencies;

        public Bootstrap(DebugSettings debugSettings)
        {
            showSplash = debugSettings.showSplash;
            showAuthentication = debugSettings.showAuthentication;
            showLoading = debugSettings.showLoading;
            enableLOD = debugSettings.enableLOD;
            enableLandscape = debugSettings.enableLandscape;
        }

        public void PreInitializeSetup(RealmLaunchSettings launchSettings, UIDocument cursorRoot, UIDocument debugUiRoot,
            GameObject splashRoot, CancellationToken _)
        {
            splashRoot.SetActive(showSplash);
            cursorRoot.EnsureNotNull();

#if !UNITY_EDITOR
            ProcessDeepLinkParameters(launchSettings);
#endif

            startingRealm = URLDomain.FromString(launchSettings.GetStartingRealm());
            startingParcel = launchSettings.TargetScene;

            // Hides the debug UI during the initial flow
            debugUiRoot.rootVisualElement.EnsureNotNull().style.display = DisplayStyle.None;

            // Initialize .NET logging ASAP since it might be used by another systems
            // Otherwise we might get exceptions in different platforms
            DotNetLoggingPlugin.Initialize();
        }

        public async UniTask<(StaticContainer?, bool)> LoadStaticContainerAsync(BootstrapContainer bootstrapContainer, PluginSettingsContainer globalPluginSettingsContainer, DebugViewsCatalog debugViewsCatalog, CancellationToken ct) =>
            await StaticContainer.CreateAsync(bootstrapContainer.AssetsProvisioner, bootstrapContainer.ReportHandlingSettings, debugViewsCatalog, globalPluginSettingsContainer,
                bootstrapContainer.IdentityCache, bootstrapContainer.Web3VerifiedAuthenticator, ct);

        public async UniTask<(DynamicWorldContainer?, bool)> LoadDynamicWorldContainerAsync(BootstrapContainer bootstrapContainer, StaticContainer staticContainer,
            PluginSettingsContainer scenePluginSettingsContainer, DynamicSceneLoaderSettings settings, DynamicSettings dynamicSettings, RealmLaunchSettings launchSettings,
            UIDocument uiToolkitRoot, UIDocument cursorRoot, Animator splashScreenAnimation, AudioClipConfig backgroundMusic, WorldInfoTool worldInfoTool,
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
                SplashAnimator = splashScreenAnimation,
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
                    EnableLandscape = enableLandscape,
                    EnableLOD = enableLOD,
                    EnableAnalytics = EnableAnalytics, HybridSceneParams = launchSettings.CreateHybridSceneParams(startingParcel)
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

        public async UniTask InitializeFeatureFlagsAsync(IWeb3Identity? identity, StaticContainer staticContainer, CancellationToken ct)
        {
            try { await staticContainer.FeatureFlagsProvider.InitializeAsync(identity?.Address, ct); }
            catch (Exception e) when (e is not OperationCanceledException) { ReportHub.LogException(e, new ReportData(ReportCategory.FEATURE_FLAGS)); }
        }

        public (GlobalWorld, Entity) CreateGlobalWorldAndPlayer(BootstrapContainer bootstrapContainer, StaticContainer staticContainer, DynamicWorldContainer dynamicWorldContainer,
            UIDocument debugUiRoot)
        {
            Entity playerEntity;
            GlobalWorld globalWorld;

            var sceneSharedContainer = SceneSharedContainer.Create(
                in staticContainer,
                dynamicWorldContainer.MvcManager,
                bootstrapContainer.IdentityCache,
                dynamicWorldContainer.ProfileRepository,
                staticContainer.WebRequestsContainer.WebRequestController,
                dynamicWorldContainer.RoomHub,
                dynamicWorldContainer.RealmController.RealmData,
                dynamicWorldContainer.MessagePipesHub
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
            GlobalWorld? globalWorld, Entity playerEntity, Animator splashScreenAnimation, GameObject splashRoot, CancellationToken ct)
        {
            if (showSplash)
                await UniTask.WaitUntil(() => splashScreenAnimation.GetCurrentAnimatorStateInfo(0).normalizedTime > 1, cancellationToken: ct);

            splashScreenAnimation.transform.SetSiblingIndex(1);

            await dynamicWorldContainer.UserInAppInitializationFlow.ExecuteAsync(showAuthentication, showLoading, false,
                globalWorld!.EcsWorld, playerEntity, ct);

            splashRoot.SetActive(false);
            OpenDefaultUI(dynamicWorldContainer.MvcManager, ct);
        }

        private void ProcessDeepLinkParameters(RealmLaunchSettings launchSettings)
        {
            string deepLinkString = string.Empty;

#if UNITY_STANDALONE_WIN
            // When started in local scene development mode (AKA preview mode) command line arguments are used
            // Example (Windows) -> start decentraland://"realm=http://127.0.0.1:8000&position=100,100&otherparam=blahblah"
            string[] cmdArgs = Environment.GetCommandLineArgs();

            if (cmdArgs.Length <= 1) return; // first cmd arg is always the application path

            foreach (string cmdArg in cmdArgs)
            {
                if (cmdArg.StartsWith("decentraland://"))
                {
                    deepLinkString = cmdArg;
                    break;
                }
            }
#else
            // Patch for MacOS removing the ':' from the realm parameter protocol
            deepLinkString = Regex.Replace(Application.absoluteURL, @"(https?)//(.*?)$", @"$1://$2");
#endif

            if (string.IsNullOrEmpty(deepLinkString)) return;

            // Update deep link so that Uri class allows the host name
            deepLinkString = Regex.Replace(deepLinkString, @"^decentraland:/+", "https://decentraland.com/?");

            if (!Uri.TryCreate(deepLinkString, UriKind.Absolute, out var res)) return;

            var uri = new Uri(deepLinkString);
            var uriQuery = HttpUtility.ParseQueryString(uri.Query);

            var realmParam = uriQuery.Get("realm");
            if (!string.IsNullOrEmpty(realmParam))
            {
                localSceneDevelopment = ParseLocalSceneParameter(uriQuery);

                if (localSceneDevelopment && IsRealmALocalUrl(realmParam))
                    launchSettings.SetLocalSceneDevelopmentRealm(realmParam);
                else if (IsRealmAWorld(realmParam))
                    launchSettings.SetWorldRealm(realmParam);
            }

            Vector2Int targetPosition = ParsePositionParameter(uriQuery);

            launchSettings.SetTargetScene(targetPosition);
        }

        private bool ParseLocalSceneParameter(NameValueCollection uriQuery)
        {
            bool returnValue = false;
            var localSceneParam = uriQuery.Get("local-scene");
            if (!string.IsNullOrEmpty(localSceneParam))
            {
                var match = new Regex(@"true|false").Match(localSceneParam);
                if (match.Success)
                    bool.TryParse(match.Value, out returnValue);
            }

            return returnValue;
        }

        private bool IsRealmAWorld(string realmParam) =>
            new Regex(@"^[a-zA-Z0-9.]+\.eth$").Match(realmParam).Success;

        private bool IsRealmALocalUrl(string realmParam) =>
            Uri.TryCreate(realmParam, UriKind.Absolute, out var uriResult)
            && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);

        private Vector2Int ParsePositionParameter(NameValueCollection uriQuery)
        {
            Vector2Int returnValue = Vector2Int.zero;
            var positionParam = uriQuery.Get("position");
            if (!string.IsNullOrEmpty(positionParam))
            {
                var matches = new Regex(@"-*\d+").Matches(positionParam);
                if (matches.Count > 1)
                {
                    returnValue.x = int.Parse(matches[0].Value);
                    returnValue.y = int.Parse(matches[1].Value);
                }
            }
            return returnValue;
        }

        private static void OpenDefaultUI(IMVCManager mvcManager, CancellationToken ct)
        {
            // TODO: all of these UIs should be part of a single canvas. We cannot make a proper layout by having them separately
            mvcManager.ShowAsync(MinimapController.IssueCommand(), ct).Forget();
            mvcManager.ShowAsync(PersistentExplorePanelOpenerController.IssueCommand(new EmptyParameter()), ct).Forget();
            mvcManager.ShowAsync(ChatController.IssueCommand(), ct).Forget();
            mvcManager.ShowAsync(NewNotificationController.IssueCommand(), ct).Forget();
            mvcManager.ShowAsync(PersistentEmoteWheelOpenerController.IssueCommand(), ct).Forget();
        }
    }
}
