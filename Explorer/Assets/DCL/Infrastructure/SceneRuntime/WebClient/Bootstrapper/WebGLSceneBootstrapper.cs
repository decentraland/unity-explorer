using Arch.Core;
using Arch.SystemGroups;
using CommunicationData.URLHelpers;
using CRDT.Serializer;
using CrdtEcsBridge.Components;
using CrdtEcsBridge.PoolsProviders;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision.CodeResolver;
using DCL.Interaction.Utility;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.Multiplayer.Profiles.Poses;
using DCL.Profiles;
using DCL.SkyBox;
using DCL.Web3;
using DCL.Web3.Identities;
using DCL.WebRequests;
using ECS;
using ECS.LifeCycle;
using ECS.Prioritization.Components;
using DCL.WebRequests.Analytics;
using DCL.WebRequests.ChromeDevtool;
using DCL.WebRequests.RequestsHub;
using Global.AppArgs;
using Global.Dynamic;
using MVC;
using PortableExperiences.Controller;
using SceneRunner;
using SceneRunner.ECSWorld;
using SceneRunner.Scene;
using SceneRuntime.Factory;
using SceneRuntime.Factory.WebSceneSource;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Threading;
using System.Web;
using UnityEngine;
using DCL.Clipboard;
using CrdtEcsBridge.JsModulesImplementation.Communications;
using DCL.DebugUtilities.UIBindings;
using DCL.PluginSystem.World;
using DCL.Utility.Types;
using ECS.SceneLifeCycle.Realm;

namespace SceneRuntime.WebClient.Bootstrapper
{
    public class WebGLSceneBootstrapper : MonoBehaviour
    {
        private const string SCENE_URL_ARG = "sceneUrl";
        private const string SCENE_DIRECTORY = "cube-wave-16x16";

        private static string GetDefaultSceneUrl()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            // In WebGL, Application.streamingAssetsPath already returns a full URL like "http://localhost:8800/StreamingAssets/"
            try
            {
                string streamingPath = Application.streamingAssetsPath;

                Debug.Log($"[WebGLSceneBootstrapper] StreamingAssets Path: {streamingPath}");

                if (!string.IsNullOrEmpty(streamingPath))
                {
                    // Ensure trailing slash
                    if (!streamingPath.EndsWith("/"))
                        streamingPath += "/";

                    // Use the scene root as base, with the full path to the JS file
                    // This matches how scene.json expects paths: "main": "bin/game.js"
                    string fullUrl = $"{streamingPath}Scenes/{SCENE_DIRECTORY}/bin/game.js";
                    Debug.Log($"[WebGLSceneBootstrapper] Constructed scene URL: {fullUrl}");
                    return fullUrl;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[WebGLSceneBootstrapper] Failed to construct StreamingAssets URL: {e.Message}\n{e.StackTrace}");
            }

            // Fallback: use localhost
            Debug.LogWarning($"[WebGLSceneBootstrapper] Using fallback localhost URL");
            return IRealmNavigator.LOCALHOST + "/index.js";
#else
            // Fallback for editor/other platforms
            return IRealmNavigator.LOCALHOST + "/index.js";
#endif
        }

        private ISceneFacade? sceneFacade;
        private bool isInitialized;
        private bool hasError;
        private string? errorMessage;

        private void Start()
        {
#if !UNITY_WEBGL
            hasError = true;
            errorMessage = "WebGLSceneBootstrapper only works in WebGL builds. WebClientJavaScriptEngine requires browser JavaScript runtime.";
            Debug.LogError(errorMessage);
            enabled = false;
            return;
#endif

            InitializeAsync().Forget();
        }

        private async UniTaskVoid InitializeAsync()
        {
            try
            {
                // Parse command line arguments (works for desktop builds)
                var appArgs = new ApplicationParametersParser();
                string sceneUrl = GetDefaultSceneUrl();

                // Try to get from command line arguments first
                if (appArgs.TryGetValue(SCENE_URL_ARG, out string? urlArg) && !string.IsNullOrEmpty(urlArg))
                {
                    sceneUrl = urlArg;
                }
                else
                {
                    // For WebGL, also check URL query parameters
                    sceneUrl = GetSceneUrlFromQueryString() ?? sceneUrl;
                }

                Debug.Log($"[WebGLSceneBootstrapper] Loading scene from URL: {sceneUrl}");

                // Validate URL format
                if (!Uri.TryCreate(sceneUrl, UriKind.Absolute, out Uri? validatedUri))
                {
                    throw new ArgumentException($"Invalid scene URL format: {sceneUrl}");
                }

                Debug.Log($"[WebGLSceneBootstrapper] URL validated successfully. Scheme: {validatedUri.Scheme}, Host: {validatedUri.Host}, Path: {validatedUri.AbsolutePath}");

                // Initialize mocked dependencies
                var dependencies = CreateMockedDependencies();

                // Create scene factory
                var sceneFactory = CreateSceneFactory(dependencies);

                // Create partition component (required but not used in this context)
                var partitionComponent = new StubPartitionComponent();

                // Load scene from HTTP URL
                // In WebGL, StreamingAssets are served via HTTP, so we need HTTP URLs
                // But CreateSceneFromFileAsync might have issues with the URL format
                Debug.Log($"[WebGLSceneBootstrapper] Calling CreateSceneFromFileAsync with URL: {sceneUrl}");

                // Log what CreateSceneFromFileAsync will extract
                int lastSlash = sceneUrl.LastIndexOf("/", StringComparison.Ordinal);
                if (lastSlash > 0)
                {
                    string extractedBaseUrl = sceneUrl[..(lastSlash + 1)];
                    string extractedMainPath = sceneUrl[(lastSlash + 1)..];
                    Debug.Log($"[WebGLSceneBootstrapper] Will extract base URL: {extractedBaseUrl}");
                    Debug.Log($"[WebGLSceneBootstrapper] Will extract main path: {extractedMainPath}");
                }

                try
                {
                    sceneFacade = await sceneFactory.CreateSceneFromFileAsync(
                        sceneUrl,
                        partitionComponent,
                        destroyCancellationToken
                    );
                }
                catch (Exception createException)
                {
                    Debug.LogError($"[WebGLSceneBootstrapper] Error in CreateSceneFromFileAsync: {createException.GetType().Name}: {createException.Message}");
                    Debug.LogError($"[WebGLSceneBootstrapper] Stack trace: {createException.StackTrace}");
                    throw;
                }

                Debug.Log($"[WebGLSceneBootstrapper] Initializing Scene Facade");
                sceneFacade.Initialize();

                Debug.Log($"[WebGLSceneBootstrapper] Starting Scene");
                await sceneFacade.StartScene();

                isInitialized = true;
                Debug.Log($"[WebGLSceneBootstrapper] Scene from '{sceneUrl}' loaded and started successfully");
            }
            catch (Exception e)
            {
                hasError = true;
                errorMessage = $"Failed to initialize scene: {e.Message}\n{e.StackTrace}";
                Debug.LogError($"[WebGLSceneBootstrapper] {errorMessage}");
                enabled = false;
            }
        }

        private void Update()
        {
            if (hasError || !isInitialized || sceneFacade == null)
                return;

            try
            {
                sceneFacade.Tick(Time.deltaTime);
            }
            catch (Exception e)
            {
                Debug.LogError($"[WebGLSceneBootstrapper] Error in update loop: {e.Message}\n{e.StackTrace}");
            }
        }

        private void OnDestroy()
        {
            if (sceneFacade != null)
            {
                sceneFacade.DisposeAsync().Forget();
            }
        }

        private void OnGUI()
        {
            if (hasError && !string.IsNullOrEmpty(errorMessage))
            {
                GUI.color = Color.red;
                GUI.Label(new Rect(10, 10, Screen.width - 20, Screen.height - 20), errorMessage);
            }
            else if (!isInitialized)
            {
                GUI.color = Color.yellow;
                GUI.Label(new Rect(10, 10, 200, 30), "Loading scene...");
            }
        }

        private MockedDependencies CreateMockedDependencies()
        {
            // Create minimal ECS world
            var world = World.Create();
            var builder = new ArchSystemsWorldBuilder<World>(world);

            // Create minimal persistent entities
            var playerEntity = world.Create();
            var cameraEntity = world.Create();
            var sceneRootEntity = world.Create();
            var sceneContainerEntity = world.Create();
            var persistentEntities = new PersistentEntities(playerEntity, cameraEntity, sceneRootEntity, sceneContainerEntity);

            var ecsWorldFacade = new ECSWorldFacade(
                builder.Finish(),
                world,
                persistentEntities,
                new List<IFinalizeWorldSystem>(),
                new List<ISceneIsCurrentListener>()
            );

            // Create ECS world factory
            var ecsWorldFactory = new StubECSWorldFactory(ecsWorldFacade);

            return new MockedDependencies
            {
                ECSWorldFactory = ecsWorldFactory,
                SharedPoolsProvider = new SharedPoolsProvider(),
                CRDTSerializer = new CRDTSerializer(),
                SDKComponentsRegistry = new SDKComponentsRegistry(),
                EntityFactory = new SceneEntityFactory(),
                EntityCollidersGlobalCache = new EntityCollidersGlobalCache(),
                EthereumApi = new StubEthereumApi(),
                MVCManager = new StubMVCManager(),
                ProfileRepository = new ProfileRepositoryFake(),
                IdentityCache = new IWeb3IdentityCache.Default(),
                DecentralandUrlsSource = new StubDecentralandUrlsSource(),
                WebRequestController = CreateWebRequestController(),
                RoomHub = NullRoomHub.INSTANCE,
                RealmData = new IRealmData.Fake(),
                PortableExperiencesController = new StubPortableExperiencesController(),
                SkyboxSettings = ScriptableObject.CreateInstance<SkyboxSettingsAsset>(),
                MessagePipesHub = new StubSceneCommunicationPipe(),
                RemoteMetadata = new StubRemoteMetadata(),
                DCLEnvironment = DecentralandEnvironment.Org,
                SystemClipboard = new StubSystemClipboard()
            };
        }

        private static string? GetSceneUrlFromQueryString()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            try
            {
                // Get the current page URL
                string currentUrl = Application.absoluteURL;

                if (string.IsNullOrEmpty(currentUrl))
                    return null;

                // Parse query parameters
                Uri uri = new Uri(currentUrl);
                NameValueCollection queryParams = HttpUtility.ParseQueryString(uri.Query);

                // Check for sceneUrl parameter
                string? sceneUrl = queryParams[SCENE_URL_ARG];

                if (!string.IsNullOrEmpty(sceneUrl))
                {
                    // URL decode the parameter
                    return HttpUtility.UrlDecode(sceneUrl);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[WebGLSceneBootstrapper] Failed to parse URL query parameters: {e.Message}");
            }
#endif
            return null;
        }

        private static IWebRequestController CreateWebRequestController()
        {
            const int TOTAL_BUDGET = int.MaxValue;

            return new WebRequestController(
                new StubWebRequestsAnalyticsContainer(),
                new IWeb3IdentityCache.Default(),
                new RequestHub(new StubDecentralandUrlsSource()),
                Option<ChromeDevtoolProtocolClient>.None,
                new WebRequestBudget(TOTAL_BUDGET, new ElementBinding<ulong>(TOTAL_BUDGET))
            );
        }

        private SceneFactory CreateSceneFactory(MockedDependencies deps)
        {
            // Create WebClient JavaScript engine factory
            var engineFactory = new WebClientJavaScriptEngineFactory();

            // Create WebJsSources for loading JS modules
            var webJsSources = new WebJsSources(new JsCodeResolver(deps.WebRequestController));

            // Create scene runtime factory
            var sceneRuntimeFactory = new SceneRuntimeFactory(
                deps.RealmData,
                engineFactory,
                webJsSources
            );

            // Create scene factory
            return new SceneFactory(
                deps.ECSWorldFactory,
                sceneRuntimeFactory,
                deps.SharedPoolsProvider,
                deps.CRDTSerializer,
                deps.SDKComponentsRegistry,
                deps.EntityFactory,
                deps.EntityCollidersGlobalCache,
                deps.EthereumApi,
                deps.MVCManager,
                deps.ProfileRepository,
                deps.IdentityCache,
                deps.DecentralandUrlsSource,
                deps.WebRequestController,
                deps.RoomHub,
                deps.RealmData,
                deps.PortableExperiencesController,
                deps.SkyboxSettings,
                deps.MessagePipesHub,
                deps.RemoteMetadata,
                deps.DCLEnvironment,
                deps.SystemClipboard
            );
        }

        private class MockedDependencies
        {
            public IECSWorldFactory ECSWorldFactory { get; set; } = null!;
            public ISharedPoolsProvider SharedPoolsProvider { get; set; } = null!;
            public ICRDTSerializer CRDTSerializer { get; set; } = null!;
            public ISDKComponentsRegistry SDKComponentsRegistry { get; set; } = null!;
            public ISceneEntityFactory EntityFactory { get; set; } = null!;
            public IEntityCollidersGlobalCache EntityCollidersGlobalCache { get; set; } = null!;
            public IEthereumApi EthereumApi { get; set; } = null!;
            public IMVCManager MVCManager { get; set; } = null!;
            public IProfileRepository ProfileRepository { get; set; } = null!;
            public IWeb3IdentityCache IdentityCache { get; set; } = null!;
            public IDecentralandUrlsSource DecentralandUrlsSource { get; set; } = null!;
            public IWebRequestController WebRequestController { get; set; } = null!;
            public IRoomHub RoomHub { get; set; } = null!;
            public IRealmData RealmData { get; set; } = null!;
            public IPortableExperiencesController PortableExperiencesController { get; set; } = null!;
            public SkyboxSettingsAsset SkyboxSettings { get; set; } = null!;
            public ISceneCommunicationPipe MessagePipesHub { get; set; } = null!;
            public IRemoteMetadata RemoteMetadata { get; set; } = null!;
            public DecentralandEnvironment DCLEnvironment { get; set; }
            public ISystemClipboard SystemClipboard { get; set; } = null!;
        }

        private class StubSceneCommunicationPipe : ISceneCommunicationPipe
        {
            public void AddSceneMessageHandler(string sceneId, ISceneCommunicationPipe.MsgType msgType, ISceneCommunicationPipe.SceneMessageHandler onSceneMessage) { }

            public void RemoveSceneMessageHandler(string sceneId, ISceneCommunicationPipe.MsgType msgType, ISceneCommunicationPipe.SceneMessageHandler onSceneMessage) { }

            public void SendMessage(ReadOnlySpan<byte> message, string sceneId, ISceneCommunicationPipe.ConnectivityAssertiveness assertiveness, CancellationToken ct, string? specialRecipient = null) { }
        }

        // Simple stub implementations for interfaces that don't have fake implementations
        private class StubEthereumApi : IEthereumApi
        {
            public UniTask<EthApiResponse> SendAsync(EthApiRequest request, CancellationToken ct) =>
                UniTask.FromResult(new EthApiResponse {});

            public void Dispose() { }
        }

        private class StubMVCManager : IMVCManager
        {
            public event Action<IController>? OnViewShowed;
            public event Action<IController>? OnViewClosed;

            public UniTask ShowAsync<TView, TInputData>(ShowCommand<TView, TInputData> command, CancellationToken ct = default) where TView : IView
            {
                return UniTask.CompletedTask;
            }

            public void RegisterController<TView, TInputData>(IController<TView, TInputData> controller) where TView : IView { }

            public void SetAllViewsCanvasActive(bool isActive) { }

            public void SetAllViewsCanvasActive(IController except, bool isActive) { }

            public void Dispose() { }
        }

        private class StubDecentralandUrlsSource : IDecentralandUrlsSource
        {
            public string Url(DecentralandUrl decentralandUrl) => string.Empty;
            public string GetHostnameForFeatureFlag() => string.Empty;
        }

        private class StubPortableExperiencesController : IPortableExperiencesController
        {
            public event Action<string>? PortableExperienceLoaded;
            public event Action<string>? PortableExperienceUnloaded;
            public Dictionary<string, Entity> PortableExperienceEntities { get; } = new();
            public GlobalWorld GlobalWorld { get; set; } = null!;

            public bool CanKillPortableExperience(string id) => false;
            public UniTask<IPortableExperiencesController.SpawnResponse> CreatePortableExperienceByEnsAsync(ENS ens, CancellationToken ct, bool isGlobalPortableExperience = false, bool force = false)
            {
                return UniTask.FromResult(new IPortableExperiencesController.SpawnResponse());
            }
            public IPortableExperiencesController.ExitResponse UnloadPortableExperienceById(string id) => new IPortableExperiencesController.ExitResponse { status = false };
            public List<IPortableExperiencesController.SpawnResponse> GetAllPortableExperiences() => new List<IPortableExperiencesController.SpawnResponse>();
            public void UnloadAllPortableExperiences() { }
            public void AddPortableExperience(string id, Entity portableExperience) { }
        }

        private class StubRemoteMetadata : IRemoteMetadata
        {
            public IReadOnlyDictionary<string, IRemoteMetadata.ParticipantMetadata> Metadata { get; } = new Dictionary<string, IRemoteMetadata.ParticipantMetadata>();
            public void BroadcastSelfParcel(Vector2Int pose) { }
            public void BroadcastSelfMetadata() { }
        }

        private class StubSystemClipboard : ISystemClipboard
        {
            public void SetText(string text) { }
            public string GetText() => string.Empty;

            public void Set(string text)
            {
            }

            public string Get() =>
                string.Empty;

            public bool HasValue() =>
                false;
        }

        private class StubPartitionComponent : IPartitionComponent
        {
            public byte Bucket => 0;
            public bool IsBehind => false;
            public bool IsDirty => false;
            public float RawSqrDistance => 0f;
        }

        private class StubECSWorldFactory : IECSWorldFactory
        {
            private readonly ECSWorldFacade worldFacade;

            public StubECSWorldFactory(ECSWorldFacade worldFacade)
            {
                this.worldFacade = worldFacade;
            }

            public ECSWorldFacade CreateWorld(in ECSWorldFactoryArgs args) => worldFacade;
        }

        private class StubWebRequestsAnalyticsContainer : IWebRequestsAnalyticsContainer
        {
            public IDictionary<Type, Func<IRequestMetric>> GetTrackedMetrics() => new Dictionary<Type, Func<IRequestMetric>>();
            public IReadOnlyList<IRequestMetric>? GetMetric(Type requestType) => null;
            void IWebRequestsAnalyticsContainer.OnRequestStarted<T>(T request) { }
            void IWebRequestsAnalyticsContainer.OnRequestFinished<T>(T request) { }
            void IWebRequestsAnalyticsContainer.OnProcessDataStarted<T>(T request) { }
            void IWebRequestsAnalyticsContainer.OnProcessDataFinished<T>(T request) { }
        }
    }
}
