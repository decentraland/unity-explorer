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
using DCL.UI.Sidebar;
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
using System.Text.RegularExpressions;
using System.Web;

namespace Global.Dynamic
{
    public class Bootstrap : IBootstrap
    {
        private const string APP_PARAMETER_REALM = "realm";
        private const string APP_PARAMETER_LOCAL_SCENE = "local-scene";
        private const string APP_PARAMETER_POSITION = "position";

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
        private Dictionary<string, string> appParameters = new Dictionary<string, string>();

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

            appParameters = ParseApplicationParameters();

            if (appParameters.ContainsKey(APP_PARAMETER_REALM))
                ProcessRealmAppParameter(launchSettings);

            if (appParameters.ContainsKey(APP_PARAMETER_POSITION))
                ProcessPositionAppParameter(appParameters[APP_PARAMETER_POSITION], launchSettings);

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
                    EnableAnalytics = EnableAnalytics, HybridSceneParams = launchSettings.CreateHybridSceneParams(startingParcel),
                    LocalSceneDevelopmentRealm = localSceneDevelopment ? launchSettings.GetStartingRealm() : string.Empty,
                    AppParameters = appParameters
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
            try { await staticContainer.FeatureFlagsProvider.InitializeAsync(identity?.Address, appParameters, ct); }
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

        private Dictionary<string, string> ParseApplicationParameters()
        {
            string[] cmdArgs = Environment.GetCommandLineArgs();

            bool deepLinkFound = false;
            string lastKeyStored = string.Empty;
            for (int i = 1; i < cmdArgs.Length; i++)
            {
                var arg = cmdArgs[i];

                if (arg.StartsWith("--"))
                {
                    if (arg.Length > 2)
                    {
                        lastKeyStored = arg.Substring(2);
                        appParameters[lastKeyStored] = string.Empty;
                    }
                    else
                        lastKeyStored = string.Empty;
                }
#if !UNITY_EDITOR && UNITY_STANDALONE_WIN
                else if (!deepLinkFound && arg.StartsWith("decentraland://"))
                {
                    deepLinkFound = true;
                    lastKeyStored = string.Empty;

                    // When started in local scene development mode (AKA preview mode) command line arguments are used
                    // Example (Windows) -> start decentraland://"realm=http://127.0.0.1:8000&position=100,100&otherparam=blahblah"
                    ProcessDeepLinkParameters(arg);
                }
#endif
                else if (!string.IsNullOrEmpty(lastKeyStored))
                    appParameters[lastKeyStored] = arg;
            }

            // in MacOS the deep link string doesn't come in the cmd args...
#if !UNITY_EDITOR && UNITY_STANDALONE_OSX
            if (!string.IsNullOrEmpty(Application.absoluteURL) && Application.absoluteURL.StartsWith("decentraland"))
            {
                // Regex patch for MacOS removing the ':' from the realm parameter protocol
                ProcessDeepLinkParameters(Regex.Replace(Application.absoluteURL, @"(https?)//(.*?)$", @"$1://$2"));
            }
#endif

            return appParameters;
        }

        private void ProcessDeepLinkParameters(string deepLinkString)
        {
            // Update deep link so that Uri class allows the host name
            deepLinkString = Regex.Replace(deepLinkString, @"^decentraland:/+", "https://decentraland.com/?");

            if (!Uri.TryCreate(deepLinkString, UriKind.Absolute, out var res)) return;

            var uri = new Uri(deepLinkString);
            var uriQuery = HttpUtility.ParseQueryString(uri.Query);

            foreach (string uriQueryKey in uriQuery.AllKeys)
            {
                appParameters[uriQueryKey] = uriQuery.Get(uriQueryKey);
            }
        }

        private void ProcessRealmAppParameter(RealmLaunchSettings launchSettings)
        {
            string realmParamValue = appParameters[APP_PARAMETER_REALM];

            if (string.IsNullOrEmpty(realmParamValue)) return;

            localSceneDevelopment = appParameters.TryGetValue(APP_PARAMETER_LOCAL_SCENE, out string localSceneParamValue) && ParseLocalSceneParameter(localSceneParamValue);

            if (localSceneDevelopment && IsRealmALocalUrl(realmParamValue))
                launchSettings.SetLocalSceneDevelopmentRealm(realmParamValue);
            else if (IsRealmAWorld(realmParamValue))
                launchSettings.SetWorldRealm(realmParamValue);
        }

        private void ProcessPositionAppParameter(string positionParameterValue, RealmLaunchSettings launchSettings)
        {
            if (string.IsNullOrEmpty(positionParameterValue)) return;

            Vector2Int targetPosition = Vector2Int.zero;

            var matches = new Regex(@"-*\d+").Matches(positionParameterValue);
            if (matches.Count > 1)
            {
                targetPosition.x = int.Parse(matches[0].Value);
                targetPosition.y = int.Parse(matches[1].Value);
            }

            launchSettings.SetTargetScene(targetPosition);
        }

        private bool ParseLocalSceneParameter(string localSceneParameter)
        {
            if (string.IsNullOrEmpty(localSceneParameter)) return false;

            bool returnValue = false;
            var match = new Regex(@"true|false").Match(localSceneParameter);
            if (match.Success)
                bool.TryParse(match.Value, out returnValue);

            return returnValue;
        }

        private bool IsRealmAWorld(string realmParam) =>
            new Regex(@"^[a-zA-Z0-9.]+\.eth$").Match(realmParam).Success;

        private bool IsRealmALocalUrl(string realmParam) =>
            Uri.TryCreate(realmParam, UriKind.Absolute, out var uriResult)
            && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);

        private static void OpenDefaultUI(IMVCManager mvcManager, CancellationToken ct)
        {
            // TODO: all of these UIs should be part of a single canvas. We cannot make a proper layout by having them separately
            mvcManager.ShowAsync(SidebarController.IssueCommand(), ct).Forget();
            mvcManager.ShowAsync(MinimapController.IssueCommand(), ct).Forget();
            mvcManager.ShowAsync(ChatController.IssueCommand(), ct).Forget();
            mvcManager.ShowAsync(NewNotificationController.IssueCommand(), ct).Forget();
            mvcManager.ShowAsync(PersistentEmoteWheelOpenerController.IssueCommand(), ct).Forget();
        }
    }
}
