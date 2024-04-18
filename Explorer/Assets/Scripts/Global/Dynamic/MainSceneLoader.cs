using Arch.Core;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Audio;
using DCL.Browser;
using DCL.Chat;
using DCL.Diagnostics;
using DCL.ExplorePanel;
using DCL.Minimap;
using DCL.PluginSystem;
using DCL.PluginSystem.Global;
using DCL.Utilities;
using DCL.Web3.Authenticators;
using DCL.Web3.Identities;
using MVC;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.Video;
using Utility;

namespace Global.Dynamic
{
    public class MainSceneLoader : MonoBehaviour
    {
        private static readonly int OUT = Animator.StringToHash("Out");

        [Header("Startup Config")]
        [SerializeField] private InitialRealm initialRealm;
        [SerializeField] [ShowIfEnum("initialRealm", (int)InitialRealm.SDK)] [SDKParcelPositionHelper]
        private Vector2Int targetScene;
        [SerializeField] [ShowIfEnum("initialRealm", (int)InitialRealm.World)] private string targetWorld = "MetadyneLabs.dcl.eth";
        [SerializeField] [ShowIfEnum("initialRealm", (int)InitialRealm.Custom)] private string customRealm = "https://peer.decentraland.org";
        [SerializeField] private bool showSplash;
        [SerializeField] private bool showAuthentication;
        [SerializeField] private bool showLoading;
        [SerializeField] private bool enableLandscape;

        [Header("References")]
        [SerializeField] private PluginSettingsContainer globalPluginSettingsContainer = null!;
        [SerializeField] private PluginSettingsContainer scenePluginSettingsContainer = null!;
        [SerializeField] private UIDocument uiToolkitRoot = null!;
        [SerializeField] private UIDocument cursorRoot = null!;
        [SerializeField] private UIDocument debugUiRoot = null!;
        [SerializeField] private DynamicSceneLoaderSettings settings = null!;
        [SerializeField] private DynamicSettings dynamicSettings = null!;
        [SerializeField] private GameObject splashRoot = null!;
        [SerializeField] private Animator splashScreenAnimation = null!;
        [SerializeField] private VideoPlayer splashAnimation = null!;
        [SerializeField] private AudioClipConfig backgroundMusic;

        private DynamicWorldContainer? dynamicWorldContainer;
        private GlobalWorld? globalWorld;
        private IWeb3IdentityCache? identityCache;
        private SceneSharedContainer? sceneSharedContainer;
        private StaticContainer? staticContainer;
        private IWeb3VerifiedAuthenticator? web3Authenticator;
        private DappWeb3Authenticator? web3VerifiedAuthenticator;
        private string startingRealm = "https://peer.decentraland.org";
        private Vector2Int startingParcel;

        private void Awake()
        {
            SetupInitialConfig();

            InitializeFlowAsync(destroyCancellationToken).Forget();
        }

        private void OnDestroy()
        {
            web3Authenticator.SafeDispose(ReportCategory.AUTHENTICATION);

            if (dynamicWorldContainer != null)
            {
                foreach (IDCLGlobalPlugin plugin in dynamicWorldContainer.GlobalPlugins)
                    plugin.SafeDispose(ReportCategory.ENGINE);

                if (globalWorld != null)
                    dynamicWorldContainer.RealmController.DisposeGlobalWorld();

                dynamicWorldContainer.SafeDispose(ReportCategory.ENGINE);
            }

            if (staticContainer != null)
            {
                // Exclude SharedPlugins as they were disposed as they were already disposed of as `GlobalPlugins`
                foreach (IDCLPlugin worldPlugin in staticContainer.ECSWorldPlugins.Except<IDCLPlugin>(staticContainer.SharedPlugins))
                    worldPlugin.SafeDispose(ReportCategory.ENGINE);

                staticContainer.SafeDispose(ReportCategory.ENGINE);
            }
        }

        private async UniTask InitializeFlowAsync(CancellationToken ct)
        {
#if !UNITY_EDITOR
#if !DEVELOPMENT_BUILD

            // To avoid configuration issues, force full flow on build
            showSplash = true;
            showAuthentication = true;
            showLoading = true;
#endif

            //enableLandscape = true;
#endif

            try
            {
                splashRoot.SetActive(showSplash);

                identityCache = new LogWeb3IdentityCache(
                    new ProxyIdentityCache(
                        new MemoryWeb3IdentityCache(),
                        new PlayerPrefsIdentityProvider(
                            new PlayerPrefsIdentityProvider.DecentralandIdentityWithNethereumAccountJsonSerializer()
                        )
                    )
                );

#if !UNITY_EDITOR
                string authServerUrl = Debug.isDebugBuild
                    ? settings.AuthWebSocketUrlDev
                    : settings.AuthWebSocketUrl;

                string authSignatureUrl = Debug.isDebugBuild
                    ? settings.AuthSignatureUrlDev
                    : settings.AuthSignatureUrl;
#else
                string authServerUrl = settings.AuthWebSocketUrl;
                string authSignatureUrl = settings.AuthSignatureUrl;
#endif

                web3VerifiedAuthenticator = new DappWeb3Authenticator(new UnityAppWebBrowser(),
                    authServerUrl,
                    authSignatureUrl,
                    identityCache,
                    new HashSet<string>(settings.Web3WhitelistMethods));

                web3Authenticator = new ProxyVerifiedWeb3Authenticator(
                    web3VerifiedAuthenticator,
                    identityCache);

                // First load the common global plugin
                bool isLoaded;

                (staticContainer, isLoaded) = await StaticContainer.CreateAsync(globalPluginSettingsContainer, identityCache, web3VerifiedAuthenticator, ct);

                if (!isLoaded)
                {
                    GameReports.PrintIsDead();
                    return;
                }

                bool shouldEnableLandscape = initialRealm == InitialRealm.GenesisCity && enableLandscape;

                (dynamicWorldContainer, isLoaded) = await DynamicWorldContainer.CreateAsync(
                    new DynamicWorldDependencies
                    {
                        StaticContainer = staticContainer!,
                        SettingsContainer = scenePluginSettingsContainer,
                        RootUIDocument = uiToolkitRoot,
                        CursorUIDocument = cursorRoot,
                        DynamicSettings = dynamicSettings,
                        Web3Authenticator = web3Authenticator,
                        Web3IdentityCache = identityCache,
                    },
                    new DynamicWorldParams
                    {
                        StaticLoadPositions = settings.StaticLoadPositions,
                        Realms = settings.Realms,
                        StartParcel = startingParcel,
                        EnableLandscape = shouldEnableLandscape,
                    }, ct
                );

                if (!isLoaded)
                {
                    GameReports.PrintIsDead();
                    return;
                }

                var webRequestController = staticContainer!.WebRequestsContainer.WebRequestController;
                var roomHub = dynamicWorldContainer!.RoomHub;

                sceneSharedContainer = SceneSharedContainer.Create(in staticContainer!, dynamicWorldContainer!.MvcManager,
                    identityCache, dynamicWorldContainer.ProfileRepository, webRequestController, roomHub, dynamicWorldContainer.RealmController.GetRealm(), dynamicWorldContainer.MessagePipesHub);

                // Initialize global plugins
                var anyFailure = false;

                void OnPluginInitialized<TPluginInterface>((TPluginInterface plugin, bool success) result) where TPluginInterface: IDCLPlugin
                {
                    if (!result.success)
                        anyFailure = true;
                }

                await UniTask.WhenAll(staticContainer!.ECSWorldPlugins.Select(gp => scenePluginSettingsContainer.InitializePluginAsync(gp, ct).ContinueWith(OnPluginInitialized)));
                await UniTask.WhenAll(dynamicWorldContainer!.GlobalPlugins.Select(gp => globalPluginSettingsContainer.InitializePluginAsync(gp, ct).ContinueWith(OnPluginInitialized)));

                if (anyFailure)
                {
                    GameReports.PrintIsDead();
                    return;
                }

                UIAudioEventsBus.Instance.SendPlayLoopingAudioEvent(backgroundMusic);

                Entity playerEntity;

                (globalWorld, playerEntity) = dynamicWorldContainer!.GlobalWorldFactory.Create(sceneSharedContainer!.SceneFactory,
                    dynamicWorldContainer.EmptyScenesWorldFactory);

                dynamicWorldContainer.DebugContainer.Builder.Build(debugUiRoot);
                dynamicWorldContainer.RealmController.GlobalWorld = globalWorld;

                await ChangeRealmAsync(ct);

                if (showSplash)
                    await WaitUntilSplashAnimationEndsAsync(ct);

                await dynamicWorldContainer!.UserInAppInitializationFlow.ExecuteAsync(showAuthentication, showLoading,
                    globalWorld.EcsWorld, playerEntity, ct);

                splashScreenAnimation.SetTrigger(OUT);
                await UniTask.Delay(200);
                splashRoot.SetActive(false);

                UIAudioEventsBus.Instance.SendStopPlayingLoopingAudioEvent(backgroundMusic);
                OpenDefaultUI(dynamicWorldContainer.MvcManager, ct);
            }
            catch (OperationCanceledException)
            {
                // ignore
            }
            catch (Exception)
            {
                // unhandled exception
                GameReports.PrintIsDead();
                throw;
            }
        }

        private void SetupInitialConfig()
        {
            startingRealm = initialRealm switch
                            {
                                InitialRealm.GenesisCity => "https://peer.decentraland.org",
                                InitialRealm.SDK => "https://sdk-team-cdn.decentraland.org/ipfs/sdk7-test-scenes-main-latest",
                                InitialRealm.World => "https://worlds-content-server.decentraland.org/world/" + targetWorld,
                                InitialRealm.Localhost => "http://127.0.0.1:8000",
                                InitialRealm.Custom => customRealm,
                                _ => startingRealm,
                            };

            startingParcel = initialRealm == InitialRealm.SDK ? targetScene : settings.StartPosition;
        }

        private void OpenDefaultUI(IMVCManager mvcManager, CancellationToken ct)
        {
            mvcManager.ShowAsync(MinimapController.IssueCommand(), ct).Forget();
            mvcManager.ShowAsync(PersistentExplorePanelOpenerController.IssueCommand(new EmptyParameter()), ct).Forget();
            mvcManager.ShowAsync(ChatController.IssueCommand(), ct).Forget();
        }

        private async UniTask WaitUntilSplashAnimationEndsAsync(CancellationToken ct)
        {
            await UniTask.WaitUntil(() => splashAnimation.frame >= (long)(splashAnimation.frameCount - 1),
                cancellationToken: ct);
        }

        private async UniTask ChangeRealmAsync(CancellationToken ct)
        {
            IRealmController realmController = dynamicWorldContainer!.RealmController;
            await realmController.SetRealmAsync(URLDomain.FromString(startingRealm), ct);
        }

        [ContextMenu(nameof(ValidateSettingsAsync))]
        public async UniTask ValidateSettingsAsync()
        {
            using var scope = new CheckingScope(ReportData.UNSPECIFIED);

            await UniTask.WhenAll(
                globalPluginSettingsContainer.EnsureValidAsync(),
                scenePluginSettingsContainer.EnsureValidAsync()
            );

            ReportHub.Log(ReportData.UNSPECIFIED, "Success checking");
        }

        private readonly struct CheckingScope : IDisposable
        {
            private readonly ReportData data;

            public CheckingScope(ReportData data)
            {
                this.data = data;
                ReportHub.Log(data, "Start checking");
            }

            public void Dispose()
            {
                ReportHub.Log(data, "Finish checking");
            }
        }
    }
}
