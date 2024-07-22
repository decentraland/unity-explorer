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
using System.Threading;
using UnityEngine;
using UnityEngine.UIElements;
using System.Text.RegularExpressions;

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

        private string startingRealm = IRealmNavigator.GENESIS_URL;
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

            localSceneDevelopment = DetectAndConfigureLocalSceneDevelopment(launchSettings);

            startingRealm = launchSettings.GetStartingRealm();
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

            (var dynamicWorldContainer, bool loaded) =  await DynamicWorldContainer.CreateAsync(
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

            if (loaded && localSceneDevelopment)
                dynamicWorldContainer!.LocalSceneDevelopmentController.Initialize(launchSettings.GetStartingRealm());

            return (dynamicWorldContainer, loaded);
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

        public async UniTask InitializeFeatureFlagsAsync(IWeb3Identity identity, StaticContainer staticContainer, CancellationToken ct)
        {
            try { await staticContainer.FeatureFlagsProvider.InitializeAsync(identity.Address, ct); }
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
                dynamicWorldContainer.RealmController.GetRealm(),
                dynamicWorldContainer.MessagePipesHub
            );

            (globalWorld, playerEntity) = dynamicWorldContainer.GlobalWorldFactory.Create(sceneSharedContainer.SceneFactory);
            dynamicWorldContainer.RealmController.GlobalWorld = globalWorld;

            staticContainer.DebugContainerBuilder.BuildWithFlex(debugUiRoot);

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

            await dynamicWorldContainer.UserInAppInitializationFlow.ExecuteAsync(showAuthentication, showLoading,
                globalWorld!.EcsWorld, playerEntity, ct);

            splashRoot.SetActive(false);
            OpenDefaultUI(dynamicWorldContainer.MvcManager, ct);
        }

        private bool DetectAndConfigureLocalSceneDevelopment(RealmLaunchSettings launchSettings)
        {
            // When started in local scene development mode (AKA preview mode) a command line argument is used
            // Example (Windows) -> start decentraland://"realm=http://127.0.0.1:8000&position=100,100&otherparam=blahblah"

            // FOR DEBUGGING IN UNITY ONLY (REMOVE BEFORE REVIEW)
            // string[] cmdArgs = new[] { "", "decentraland://realm=http://127.0.0.1:8000&position=0,0" };
            string[] cmdArgs = System.Environment.GetCommandLineArgs();

            if (cmdArgs.Length > 1)
            {
                // Regex to detect different parameters in Uri based on first param after '//' and then separated by '&'
                var pattern = @"(?<=://|&)[^?&]+=[^&]+";
                var regex = new Regex(pattern);
                var matches = regex.Matches(cmdArgs[1]);

                if (matches.Count == 0 || !matches[0].Value.Contains("realm=http://127.0.0.1:"))
                    return false;

                string localRealm = matches[0].Value.Replace("realm=", "");
                launchSettings.SetLocalSceneDevelopmentRealm(localRealm);

                var positionParam = "position=";
                if (matches.Count > 1)
                {
                    for (var i = 1; i < matches.Count; i++)
                    {
                        string param = matches[i].Value;

                        if (param.Contains(positionParam))
                        {
                            param = param.Replace(positionParam, "");

                            launchSettings.SetTargetScene(new Vector2Int(
                                int.Parse(param.Substring(0, param.IndexOf(','))),
                                int.Parse(param.Substring(param.IndexOf(',') + 1))));
                        }
                    }
                }

                return true;
            }

            return false;
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
