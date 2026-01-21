using CommunicationData.URLHelpers;
using CRDT.Serializer;
using CrdtEcsBridge.Components;
using CrdtEcsBridge.PoolsProviders;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision.CodeResolver;
using DCL.CharacterCamera;
using DCL.Diagnostics;
using DCL.Interaction.Utility;
using DCL.Ipfs;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.Multiplayer.Profiles.Poses;
using DCL.Optimization.PerformanceBudgeting;
using DCL.PluginSystem.World;
using DCL.PluginSystem.World.Dependencies;
using DCL.Profiles;
using DCL.SkyBox;
using DCL.Web3;
using DCL.Web3.Identities;
using DCL.WebRequests;
using ECS;
using ECS.Prioritization;
using DCL.WebRequests.ChromeDevtool;
using DCL.WebRequests.RequestsHub;
using Global;
using Global.AppArgs;
using MVC;
using PortableExperiences.Controller;
using SceneRunner;
using SceneRunner.ECSWorld;
using SceneRunner.Scene;
using SceneRuntime.Factory;
using SceneRuntime.Factory.WebSceneSource;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;
using DCL.Clipboard;
using CrdtEcsBridge.JsModulesImplementation.Communications;
using DCL.CommunicationData.URLHelpers;
using DCL.DebugUtilities.UIBindings;
using DCL.Utility.Types;
using ECS.SceneLifeCycle.Realm;
using SceneRuntime.ScenePermissions;
using System.Collections.Specialized;
using System.Web;
using Utility;

namespace SceneRuntime.WebClient.Bootstrapper
{
    public class WebGLSceneBootstrapper : MonoBehaviour
    {
        private const string SCENE_URL_ARG = "sceneUrl";
        private const string WORLD_ARG = "world";
        private const string SCENE_DIRECTORY = "cube-wave-16x16";
        private const string WORLDS_CONTENT_SERVER = "https://worlds-content-server.decentraland.org/world/";
        private const string WORLDS_CONTENT_URL = "https://worlds-content-server.decentraland.org/contents/";

        private static string GetDefaultSceneUrl()
        {
#if UNITY_WEBGL
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
                    var fullUrl = $"{streamingPath}Scenes/{SCENE_DIRECTORY}/bin/game.js";
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
                // Check for world parameter first (e.g., ?world=olavra.dcl.eth)
                string? worldName = GetWorldFromQueryString();
                IRealmData realmData;
                ServerAbout? serverAbout = null;

                // Determine realm data based on loading mode
                if (!string.IsNullOrEmpty(worldName))
                {
                    Debug.Log($"[WebGLSceneBootstrapper] World parameter found: {worldName}");

                    // Normalize world name
                    if (!worldName.IsEns() && !worldName.EndsWith(".dcl.eth", StringComparison.OrdinalIgnoreCase))
                        worldName += ".dcl.eth";
                    worldName = worldName.ToLower();

                    // Fetch about file first to create proper RealmData
                    var aboutUrl = URLAddress.FromString($"{WORLDS_CONTENT_SERVER}{worldName}/about");
                    Debug.Log($"[WebGLSceneBootstrapper] Fetching about from: {aboutUrl.Value}");

                    var webRequestController = CreateWebRequestController();
                    serverAbout = await webRequestController
                        .GetAsync(new CommonArguments(aboutUrl), destroyCancellationToken, ReportCategory.SCENE_LOADING)
                        .CreateFromJson<ServerAbout>(WRJsonParser.Unity);

                    Debug.Log($"[WebGLSceneBootstrapper] About fetched. Realm: {serverAbout.configurations.realmName}");

                    // Create proper RealmData for the world
                    var worldIpfsRealm = new WebClientStubImplementations.WorldIpfsRealm(worldName, serverAbout);
                    var worldRealmData = new RealmData();
                    worldRealmData.Reconfigure(
                        worldIpfsRealm,
                        serverAbout.configurations.realmName ?? worldName,
                        serverAbout.configurations.networkId,
                        serverAbout.comms?.adapter ?? "",
                        serverAbout.comms?.protocol ?? "v3",
                        $"worlds-content-server.decentraland.org/world/{worldName}",
                        false
                    );
                    realmData = worldRealmData;
                }
                else
                {
                    // Use fake realm data for local scene development
                    realmData = new IRealmData.Fake(
                        new LocalIpfsRealm(URLDomain.FromString("https://localhost:8800/")),
                        false,
                        "local",
                        true,
                        1,
                        "",
                        "v3",
                        "localhost"
                    );
                }

                // Initialize mocked dependencies with proper realm data
                var dependencies = CreateMockedDependencies(realmData);

                // Create scene factory
                var sceneFactory = CreateSceneFactory(dependencies);

                if (!string.IsNullOrEmpty(worldName) && serverAbout != null)
                {
                    try
                    {
                        sceneFacade = await LoadSceneFromWorldAsync(worldName, serverAbout, sceneFactory, destroyCancellationToken);
                    }
                    catch (Exception worldException)
                    {
                        Debug.LogError($"[WebGLSceneBootstrapper] Error loading world '{worldName}': {worldException.GetType().Name}: {worldException.Message}");
                        Debug.LogError($"[WebGLSceneBootstrapper] Stack trace: {worldException.StackTrace}");
                        throw;
                    }
                }
                else
                {
                    // Fallback to sceneUrl parameter or default
                    var appArgs = new ApplicationParametersParser();
                    string sceneUrl = GetDefaultSceneUrl();

                    if (appArgs.TryGetValue(SCENE_URL_ARG, out string? urlArg) && !string.IsNullOrEmpty(urlArg))
                    {
                        sceneUrl = urlArg;
                    }
                    else
                    {
                        sceneUrl = GetSceneUrlFromQueryString() ?? sceneUrl;
                    }

                    Debug.Log($"[WebGLSceneBootstrapper] Loading scene from URL: {sceneUrl}");

                    if (!Uri.TryCreate(sceneUrl, UriKind.Absolute, out Uri? validatedUri))
                    {
                        throw new ArgumentException($"Invalid scene URL format: {sceneUrl}");
                    }

                    Debug.Log($"[WebGLSceneBootstrapper] URL validated. Scheme: {validatedUri.Scheme}, Host: {validatedUri.Host}");

                    var partitionComponent = new WebClientStubImplementations.StubPartitionComponent();

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
                }

                Debug.Log($"[WebGLSceneBootstrapper] Initializing Scene Facade");
                sceneFacade.Initialize();

                // CRITICAL: Set the scene state to Running so ECS systems will update
                Debug.Log($"[WebGLSceneBootstrapper] Setting scene state to Running");
                sceneFacade.SceneStateProvider.SetRunning(new SceneEngineStartInfo(DateTime.Now, 0));

                Debug.Log($"[WebGLSceneBootstrapper] Starting Scene");
                await sceneFacade.StartScene();

                isInitialized = true;
                Debug.Log($"[WebGLSceneBootstrapper] Scene loaded and started successfully");

                // Position the camera at the scene origin (use base parcel if loaded from world)
                PositionCameraAtSceneOrigin(currentSceneBaseParcel);
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

        private void PositionCameraAtSceneOrigin(Vector2Int? baseParcel = null)
        {
            Camera mainCamera = Camera.main;
            if (mainCamera == null)
            {
                Debug.LogWarning("[WebGLSceneBootstrapper] No main camera found to reposition");
                return;
            }

            // Use base parcel coordinates if provided, otherwise default to origin
            float centerX = baseParcel.HasValue ? baseParcel.Value.x * 16 + 8 : 8f;
            float centerZ = baseParcel.HasValue ? baseParcel.Value.y * 16 + 8 : 8f;

            Vector3 sceneCenter = new Vector3(centerX, 1f, centerZ);
            Vector3 cameraPosition = new Vector3(centerX, 5f, centerZ - 13f); // Slightly elevated and back from center

            mainCamera.transform.position = cameraPosition;
            mainCamera.transform.LookAt(sceneCenter);

            Debug.Log($"[WebGLSceneBootstrapper] Repositioned camera to {cameraPosition}, looking at {sceneCenter}");
        }

        private async UniTask<ISceneFacade> LoadSceneFromWorldAsync(
            string worldName,
            ServerAbout about,
            SceneFactory sceneFactory,
            CancellationToken ct)
        {
            // World name should already be normalized by InitializeAsync

            // Step 1: Extract scene URN (about was already fetched in InitializeAsync)
            if (about.configurations.scenesUrn == null || about.configurations.scenesUrn.Count == 0)
            {
                throw new InvalidOperationException($"World '{worldName}' has no scenes defined");
            }

            string sceneUrn = about.configurations.scenesUrn[0];
            Debug.Log($"[WebGLSceneBootstrapper] Scene URN: {sceneUrn}");

            // Step 2: Parse URN to get IpfsPath
            IpfsPath ipfsPath = IpfsHelper.ParseUrn(sceneUrn);
            Debug.Log($"[WebGLSceneBootstrapper] Parsed IpfsPath: {ipfsPath}");

            // Step 3: Construct content base URL and scene definition URL
            // For worlds, content is served from worlds-content-server, NOT from about.content.publicUrl
            var contentBaseUrl = URLDomain.FromString(WORLDS_CONTENT_URL);
            URLAddress sceneDefUrl = ipfsPath.GetUrl(contentBaseUrl);

            Debug.Log($"[WebGLSceneBootstrapper] Content base URL: {contentBaseUrl.Value}");
            Debug.Log($"[WebGLSceneBootstrapper] Fetching scene definition from: {sceneDefUrl.Value}");

            // Step 4: Fetch scene definition
            var webRequestController = CreateWebRequestController();
            SceneEntityDefinition sceneDefinition = await webRequestController
                .GetAsync(new CommonArguments(sceneDefUrl), ct, ReportCategory.SCENE_LOADING)
                .CreateFromJson<SceneEntityDefinition>(WRJsonParser.Newtonsoft);

            sceneDefinition.id ??= ipfsPath.EntityId;

            Debug.Log($"[WebGLSceneBootstrapper] Scene definition fetched. Name: {sceneDefinition.GetLogSceneName()}");
            Debug.Log($"[WebGLSceneBootstrapper] Base parcel: {sceneDefinition.metadata?.scene?.DecodedBase}");
            Debug.Log($"[WebGLSceneBootstrapper] Content files: {sceneDefinition.content?.Length ?? 0}");

            // Step 5: Create SceneData with proper content
            var sceneContent = new SceneHashedContent(sceneDefinition.content, contentBaseUrl);

            Vector2Int baseParcel = sceneDefinition.metadata.scene.DecodedBase;
            IReadOnlyList<Vector2Int> parcels = sceneDefinition.metadata.scene.DecodedParcels;
            var parcelCorners = parcels.Select(ParcelMathHelper.CalculateCorners).ToList();
            ParcelMathHelper.SceneGeometry geometry = ParcelMathHelper.CreateSceneGeometry(parcelCorners, baseParcel);

            var sceneData = new SceneData(
                sceneContent,
                sceneDefinition,
                baseParcel,
                geometry,
                parcels,
                StaticSceneMessages.EMPTY,
                new ISceneData.FakeInitialSceneState()
            );

            Debug.Log($"[WebGLSceneBootstrapper] SceneData created. Base: {baseParcel}, Parcels: {parcels.Count}");

            // Step 6: Load scene via SceneFactory
            var partitionComponent = new WebClientStubImplementations.StubPartitionComponent();
            ISceneFacade facade = await sceneFactory.CreateSceneFromSceneDefinition(
                sceneData,
                new AllowEverythingJsApiPermissionsProvider(),
                partitionComponent,
                ct
            );

            // Store base parcel for camera positioning
            currentSceneBaseParcel = baseParcel;

            Debug.Log($"[WebGLSceneBootstrapper] Scene facade created successfully");
            return facade;
        }

        // Store current scene's base parcel for camera positioning
        private Vector2Int? currentSceneBaseParcel;

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

        private MockedDependencies CreateMockedDependencies(IRealmData realmData)
        {
            // Create ComponentsContainer to register all SDK components and their pools
            var componentsContainer = ComponentsContainer.Create();

            // Create stub profiler and budgets
            var stubProfiler = new WebClientStubImplementations.StubBudgetProfiler();
            var stubMemoryCap = new WebClientStubImplementations.StubSystemMemoryCap();
            var memoryThreshold = new Dictionary<MemoryUsageStatus, float>
            {
                { MemoryUsageStatus.ABUNDANCE, 0.6f },
                { MemoryUsageStatus.WARNING, 0.8f },
                { MemoryUsageStatus.FULL, 0.9f }
            };

            var frameTimeBudget = new FrameTimeCapBudget(33f, stubProfiler, () => false);
            var memoryBudget = new MemoryBudget(stubMemoryCap, stubProfiler, memoryThreshold);

            // Create ECSWorldSingletonSharedDependencies
            var singletonSharedDependencies = new ECSWorldSingletonSharedDependencies(
                componentsContainer.ComponentPoolsRegistry,
                new WebClientStubImplementations.StubReportsHandlingSettings(),
                new SceneEntityFactory(),
                new PartitionedWorldsAggregate.Factory(),
                new WebClientStubImplementations.StubReleasablePerformanceBudget(),
                frameTimeBudget,
                memoryBudget,
                new WebClientStubImplementations.StubSceneMapping()
            );

            // Create exposed transform and camera data for plugins
            var exposedPlayerTransform = new ExposedTransform();
            var exposedCameraData = new ExposedCameraData();

            // Create web request controller
            var webRequestController = CreateWebRequestController();

            // Create minimal plugins for primitive rendering
            var plugins = new List<IDCLWorldPlugin>
            {
                new TransformsPlugin(singletonSharedDependencies, exposedPlayerTransform, exposedCameraData),
                new PrimitivesRenderingPlugin(singletonSharedDependencies)
            };

            // Create real ECSWorldFactory with plugins
            var ecsWorldFactory = new ECSWorldFactory(
                singletonSharedDependencies,
                new WebClientStubImplementations.StubPartitionSettings(),
                new WebClientStubImplementations.StubCameraSamplingData(),
                plugins
            );

            return new MockedDependencies
            {
                ECSWorldFactory = ecsWorldFactory,
                SharedPoolsProvider = new SharedPoolsProvider(),
                CRDTSerializer = new CRDTSerializer(),
                SDKComponentsRegistry = componentsContainer.SDKComponentsRegistry,
                EntityFactory = new SceneEntityFactory(),
                EntityCollidersGlobalCache = new EntityCollidersGlobalCache(),
                EthereumApi = new WebClientStubImplementations.StubEthereumApi(),
                MVCManager = new WebClientStubImplementations.StubMVCManager(),
                ProfileRepository = new ProfileRepositoryFake(),
                IdentityCache = new IWeb3IdentityCache.Default(),
                DecentralandUrlsSource = new WebClientStubImplementations.StubDecentralandUrlsSource(),
                WebRequestController = webRequestController,
                RoomHub = NullRoomHub.INSTANCE,
                RealmData = realmData,
                PortableExperiencesController = new WebClientStubImplementations.StubPortableExperiencesController(),
                SkyboxSettings = ScriptableObject.CreateInstance<SkyboxSettingsAsset>(),
                MessagePipesHub = new WebClientStubImplementations.StubSceneCommunicationPipe(),
                RemoteMetadata = new WebClientStubImplementations.StubRemoteMetadata(),
                DCLEnvironment = DecentralandEnvironment.Org,
                SystemClipboard = new WebClientStubImplementations.StubSystemClipboard()
            };
        }

        private static string? GetSceneUrlFromQueryString()
        {
#if UNITY_WEBGL
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

        private static string? GetWorldFromQueryString()
        {
#if UNITY_WEBGL
            try
            {
                string currentUrl = Application.absoluteURL;

                if (string.IsNullOrEmpty(currentUrl))
                    return null;

                Uri uri = new Uri(currentUrl);
                NameValueCollection queryParams = HttpUtility.ParseQueryString(uri.Query);

                string? world = queryParams[WORLD_ARG];

                if (!string.IsNullOrEmpty(world))
                {
                    return HttpUtility.UrlDecode(world);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[WebGLSceneBootstrapper] Failed to parse world query parameter: {e.Message}");
            }
#endif
            return null;
        }

        private static IWebRequestController CreateWebRequestController()
        {
            const int TOTAL_BUDGET = int.MaxValue;

            return new WebRequestController(
                new WebClientStubImplementations.StubWebRequestsAnalyticsContainer(),
                new IWeb3IdentityCache.Default(),
                new RequestHub(new WebClientStubImplementations.StubDecentralandUrlsSource()),
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
    }
}
