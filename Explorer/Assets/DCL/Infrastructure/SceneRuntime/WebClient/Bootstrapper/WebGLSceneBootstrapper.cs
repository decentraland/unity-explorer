using Arch.Core;
using Arch.SystemGroups;
using CommunicationData.URLHelpers;
using CRDT.Serializer;
using CrdtEcsBridge.Components;
using CrdtEcsBridge.PoolsProviders;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.AssetsProvision.CodeResolver;
using DCL.Character.Plugin;
using DCL.CharacterCamera;
using DCL.CharacterCamera.Components;
using DCL.Diagnostics;
using DCL.Interaction.Utility;
using DCL.Ipfs;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.Multiplayer.Profiles.Poses;
using DCL.Optimization.PerformanceBudgeting;
using DCL.PluginSystem;
using DCL.FeatureFlags;
using DCL.PluginSystem.Global;
using DCL.PluginSystem.World;
using DCL.PluginSystem.World.Dependencies;
using DCL.Profiles;
using DCL.Time.Systems;
using DCL.ResourcesUnloading;
using DCL.SkyBox;
using DCL.Web3;
using DCL.Web3.Identities;
using DCL.WebRequests;
using ECS;
using ECS.Groups;
using ECS.Prioritization;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.Cache.Disk;
using ECS.Unity.GLTFContainer.Asset.Cache;
using DCL.WebRequests.ChromeDevtool;
using DCL.WebRequests.RequestsHub;
using Global;
using Global.AppArgs;
using Global.Dynamic.LaunchModes;
using MVC;
using PortableExperiences.Controller;
using SceneRunner;
using SceneRunner.ECSWorld;
using SceneRunner.Scene;
using SceneRuntime.Factory;
using SceneRuntime.Factory.WebSceneSource;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using UnityEngine;
using DCL.Clipboard;
using CrdtEcsBridge.JsModulesImplementation.Communications;
using DCL.CommunicationData.URLHelpers;
using DCL.DebugUtilities.UIBindings;
using DCL.AvatarRendering.Emotes;
using DCL.Input;
using DCL.Input.Component;
using DCL.CharacterCamera.Systems;
using DCL.Utility;
using DCL.Utility.Types;
using ECS.SceneLifeCycle;
using ECS.SceneLifeCycle.Realm;
using ECS.StreamableLoading.Common.Components;
using SceneRuntime.ScenePermissions;
using System.Collections.Specialized;
using System.Web;
using Utility;

namespace SceneRuntime.WebClient.Bootstrapper
{
#if false // Not used — project uses normal bootstrapper (MainSceneLoader/DynamicWorldContainer)
    public class WebGLSceneBootstrapper : MonoBehaviour
    {
        [SerializeField] private Camera MainCamera;
        [SerializeField] private PluginSettingsContainer globalPluginSettingsContainer = null!;

        private const string SCENE_URL_ARG = "sceneUrl";
        private const string WORLD_ARG = "world";
        private const string SCENE_DIRECTORY = "cube-wave-16x16";
        private const string WORLDS_CONTENT_SERVER = "https://worlds-content-server.decentraland.org/world/";
        private const string WORLDS_CONTENT_URL = "https://worlds-content-server.decentraland.org/contents/";

        // Store current scene's base parcel for camera positioning
        private Vector2Int? currentSceneBaseParcel;
        private string? errorMessage;
        private bool hasError;
        private bool isInitialized;

        private ISceneFacade? sceneFacade;

        // Held so global world and scene can read/update; initialized in InitializeAsync
        private ExposedTransform? exposedPlayerTransform;
        private ExposedCameraData? exposedCameraData;

        // Initialized in InitializeAsync (after InitializePluginAsync); used for scene WorldPlugin and global world
        private CharacterContainer? characterContainer;

        // Minimal global world for WebGL: character motion + camera; run via player loop (registered), disposed in OnDestroy
        private World? globalWorldEcs;
        private SystemGroupWorld? globalWorldSystems;

        private void Start()
        {
            InitializeAsync().Forget();
        }

        private void Update()
        {
            // Manually update minimal global world (no SystemGroupSnapshot in WebGL)
            if (globalWorldSystems != null)
            {
                try { UpdateGlobalWorldSystems(globalWorldSystems); }
                catch (Exception e) { Debug.LogError($"[WebGLSceneBootstrapper] Global world update: {e.Message}\n{e.StackTrace}"); }
            }
        }

        // Scene tick in LateUpdate so the minimal global world runs first (in Update)
        private void LateUpdate()
        {
            if (hasError || !isInitialized || sceneFacade == null)
                return;

            try { sceneFacade.Tick(Time.deltaTime); }
            catch (Exception e) { Debug.LogError($"[WebGLSceneBootstrapper] Error in update loop: {e.Message}\n{e.StackTrace}"); }
        }

        private static void UpdateGlobalWorldSystems(SystemGroupWorld worldSystems)
        {
            const BindingFlags FLAGS = BindingFlags.Instance | BindingFlags.NonPublic;
            PropertyInfo? prop = typeof(SystemGroupWorld).GetProperty("SystemGroups", FLAGS);
            if (prop?.GetValue(worldSystems) is not IReadOnlyList<Arch.SystemGroups.SystemGroup> groups)
                return;
            for (var i = 0; i < groups.Count; i++)
                groups[i].Update();
        }

        private void OnDestroy()
        {
            if (sceneFacade != null) { sceneFacade.DisposeAsync().Forget(); }

            if (globalWorldSystems != null)
            {
                globalWorldSystems.Dispose();
                globalWorldSystems = null;
            }
            globalWorldEcs?.Dispose();
            globalWorldEcs = null;
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

        private MockedDependencies CreateMockedDependencies(IRealmData realmData, ExposedTransform exposedPlayerTransform, ExposedCameraData exposedCameraData, CharacterContainer? characterContainer)
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
                { MemoryUsageStatus.FULL, 0.9f },
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

            // Create web request controller
            IWebRequestController webRequestController = CreateWebRequestController();

            // Create cache cleaner for resource management
            var cacheCleaner = new CacheCleaner(new WebClientStubImplementations.StubReleasablePerformanceBudget(), null);

            // Create GLTF container assets cache
            var gltfContainerAssetsCache = new GltfContainerAssetsCache(componentsContainer.ComponentPoolsRegistry);
            cacheCleaner.Register(gltfContainerAssetsCache);

            // Create stub implementations for scene readiness and loading status
            var sceneReadinessReportQueue = new WebClientStubImplementations.StubSceneReadinessReportQueue();
            var loadingStatus = new WebClientStubImplementations.StubLoadingStatus();

            // Configure Asset Bundle URL from realm data or use default CDN
            // For worlds, the AssetBundleRegistry may be empty, so we use the default CDN
            URLDomain assetBundleUrl = string.IsNullOrEmpty(realmData.Ipfs.AssetBundleRegistry.Value)
                ? URLDomain.FromString("https://ab-cdn.decentraland.org/")
                : realmData.Ipfs.AssetBundleRegistry;

            // Create Asset Bundles plugin for loading pre-processed assets
            var assetBundlesPlugin = new AssetBundlesPlugin(
                new WebClientStubImplementations.StubReportsHandlingSettings(),
                cacheCleaner,
                webRequestController,
                ArrayPool<byte>.Shared,
                IDiskCache<PartialLoadingState>.Null.INSTANCE, // No disk cache for WebGL
                assetBundleUrl,
                gltfContainerAssetsCache
            );

            // Create WebGL-specific GLTF Container plugin (Asset Bundle only, no raw GLTF)
            var gltfContainerPlugin = new GltfContainerPluginWebGL(
                frameTimeBudget,
                memoryBudget,
                cacheCleaner,
                sceneReadinessReportQueue,
                loadingStatus,
                gltfContainerAssetsCache
            );

            // Create plugins for scene rendering
            var plugins = new List<IDCLWorldPlugin>
            {
                new TransformsPlugin(singletonSharedDependencies, exposedPlayerTransform, exposedCameraData),
                new PrimitivesRenderingPlugin(singletonSharedDependencies),
                new VisibilityPlugin(),
                assetBundlesPlugin,
                gltfContainerPlugin
            };

            if (characterContainer != null)
                plugins.Add(characterContainer.CreateWorldPlugin(componentsContainer.ComponentPoolsRegistry));

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
                SystemClipboard = new WebClientStubImplementations.StubSystemClipboard(),
            };
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

        private static string GetDefaultSceneUrl()
        {
            // In WebGL, Application.streamingAssetsPath already returns a full URL like "http://localhost:8800/StreamingAssets/"
            try
            {
                string streamingPath = Application.streamingAssetsPath;

                // Debug.Log($"[WebGLSceneBootstrapper] StreamingAssets Path: {streamingPath}");

                if (!string.IsNullOrEmpty(streamingPath))
                {
                    // Ensure trailing slash
                    if (!streamingPath.EndsWith("/"))
                        streamingPath += "/";

                    // Use the scene root as base, with the full path to the JS file
                    // This matches how scene.json expects paths: "main": "bin/game.js"
                    var fullUrl = $"{streamingPath}Scenes/{SCENE_DIRECTORY}/bin/game.js";
                    // Debug.Log($"[WebGLSceneBootstrapper] Constructed scene URL: {fullUrl}");
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
        }

        private static string? GetSceneUrlFromQueryString()
        {
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
            return null;
        }

        private static string? GetWorldFromQueryString()
        {
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

            return null;
        }

        private async UniTaskVoid InitializeAsync()
        {
            try
            {
                // No feature-flag config in WebGL; all features disabled
                FeaturesRegistry.InitializeEmpty();

                // Preload shader bundles before any GLTF/wearable bundles so materials deserialize with correct shaders
                await PreloadShaderBundlesAsync();

                // Check for world parameter first (e.g., ?world=olavra.dcl.eth)
                string? worldName = GetWorldFromQueryString();
                IRealmData realmData;
                ServerAbout? serverAbout = null;

                // Determine realm data based on loading mode
                if (!string.IsNullOrEmpty(worldName))
                {
                    // Debug.Log($"[WebGLSceneBootstrapper] World parameter found: {worldName}");

                    // Normalize world name
                    if (!worldName.IsEns() && !worldName.EndsWith(".dcl.eth", StringComparison.OrdinalIgnoreCase))
                        worldName += ".dcl.eth";

                    worldName = worldName.ToLower();

                    // Fetch about file first to create proper RealmData
                    var aboutUrl = URLAddress.FromString($"{WORLDS_CONTENT_SERVER}{worldName}/about");

                    // Debug.Log($"[WebGLSceneBootstrapper] Fetching about from: {aboutUrl.Value}");

                    IWebRequestController webRequestController = CreateWebRequestController();

                    serverAbout = await webRequestController
                                       .GetAsync(new CommonArguments(aboutUrl), destroyCancellationToken, ReportCategory.SCENE_LOADING)
                                       .CreateFromJson<ServerAbout>(WRJsonParser.Unity);

                    // Debug.Log($"[WebGLSceneBootstrapper] About fetched. Realm: {serverAbout.configurations.realmName}");

                    // Create proper RealmData for the world
                    var worldIpfsRealm = new WorldIpfsRealm(worldName, serverAbout);
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

                // Create and hold ExposedTransform/ExposedCameraData so global world and scene can read/update
                exposedPlayerTransform = new ExposedTransform
                {
                    Position = new CanBeDirty<Vector3>(Vector3.zero),
                    Rotation = new CanBeDirty<Quaternion>(Quaternion.identity),
                };
                exposedCameraData = new ExposedCameraData
                {
                    WorldPosition = new CanBeDirty<Vector3>(Vector3.zero),
                };

                // CharacterContainer: same pattern as main app — provision CharacterObject via InitializePluginAsync
                IAssetsProvisioner assetsProvisioner = new AddressablesProvisioner();
                characterContainer = new CharacterContainer(assetsProvisioner, exposedCameraData, exposedPlayerTransform);
                if (globalPluginSettingsContainer != null)
                {
                    (_, bool charInitSuccess) = await globalPluginSettingsContainer.InitializePluginAsync(characterContainer, destroyCancellationToken);
                    if (!charInitSuccess)
                    {
                        hasError = true;
                        errorMessage = "Failed to initialize CharacterContainer (CharacterObject provision). Assign globalPluginSettingsContainer in the WebGL scene.";
                        Debug.LogError($"[WebGLSceneBootstrapper] {errorMessage}");
                        enabled = false;
                        return;
                    }
                }

                // Minimal global world: character motion + camera + ExposePlayerTransform; runs via player loop (registered)
                globalWorldEcs = World.Create();
                Entity globalPlayerEntity = globalWorldEcs.Create();
                Profile globalFakeProfile = Profile.NewRandomProfile("webgl-player");
                if (globalWorldEcs.Has<Profile>(globalPlayerEntity))
                    globalWorldEcs.Set(globalPlayerEntity, globalFakeProfile);
                else
                    globalWorldEcs.Add(globalPlayerEntity, globalFakeProfile);
                characterContainer!.InitializePlayerEntity(globalWorldEcs, globalPlayerEntity);
                Entity globalSkyboxEntity = globalWorldEcs.Create();
                globalWorldEcs.Create(new SceneShortInfo(Vector2Int.zero, "global"));
                // InputMapComponent created by InputPlugin with NONE; we UnblockInput(CAMERA|PLAYER) after plugins inject

                var globalSceneStateProvider = new SceneStateProvider();
                globalSceneStateProvider.State.Set(SceneState.Running);

                var globalComponentsContainer = ComponentsContainer.Create();
                var stubDebugBuilder = new WebClientStubImplementations.StubDebugContainerBuilder();
                var stubSceneReadiness = new WebClientStubImplementations.StubSceneReadinessReportQueue();
                var stubLandscape = new WebClientStubImplementations.StubLandscape();
                var stubScenesCache = new WebClientStubImplementations.StubScenesCache();
                var realmSamplingData = new RealmSamplingData();
                var stubAppArgs = new ApplicationParametersParser(false);

                var characterCameraPlugin = new CharacterCameraPlugin(
                    assetsProvisioner,
                    realmSamplingData,
                    exposedCameraData!,
                    stubDebugBuilder,
                    stubAppArgs);
                (_, bool cameraInitSuccess) = await globalPluginSettingsContainer.InitializePluginAsync(characterCameraPlugin, destroyCancellationToken);
                if (!cameraInitSuccess)
                {
                    hasError = true;
                    errorMessage = "Failed to initialize CharacterCameraPlugin. Ensure CharacterCameraSettings is in globalPluginSettingsContainer.";
                    Debug.LogError($"[WebGLSceneBootstrapper] {errorMessage}");
                    enabled = false;
                    return;
                }

                // Full InputPlugin: DCLInput.Enable, ApplyInputMapsSystem, UpdateCameraInputSystem, UpdateInputMovementSystem, etc.
                var stubCursor = new WebClientStubImplementations.StubCursor();
                var stubEventSystem = UnityEngine.EventSystems.EventSystem.current != null
                    ? (IEventSystem)new UnityEventSystem(UnityEngine.EventSystems.EventSystem.current)
                    : new WebClientStubImplementations.StubEventSystem();
                var stubEmotesMessageBus = new WebClientStubImplementations.StubEmotesMessageBus();
                var emotesBus = new EmotesBus();
                //var inputPlugin = new InputPlugin(stubCursor, stubEventSystem, assetsProvisioner, stubEmotesMessageBus, emotesBus, new WebClientStubImplementations.StubMVCManager());
                //(_, bool inputInitSuccess) = await globalPluginSettingsContainer.InitializePluginAsync(inputPlugin, destroyCancellationToken);
                //if (!inputInitSuccess)
                {
                    Debug.LogWarning("[WebGLSceneBootstrapper] InputPlugin init failed. Add InputSettings to globalPluginSettingsContainer for camera/movement. Camera/movement may not respond.");
                }

                var builder = new ArchSystemsWorldBuilder<World>(globalWorldEcs);
                builder.InjectCustomGroup(new SyncedPresentationSystemGroup(globalSceneStateProvider));
                builder.InjectCustomGroup(new SyncedPreRenderingSystemGroup(globalSceneStateProvider));
                UpdateTimeSystem.InjectToWorld(ref builder);
                UpdatePhysicsTickSystem.InjectToWorld(ref builder);
                var pluginArgs = new GlobalPluginArguments(globalPlayerEntity, globalSkyboxEntity);
                characterContainer.CreateGlobalPlugin().InjectToWorld(ref builder, pluginArgs);
                //if (inputInitSuccess)
                  //  inputPlugin.InjectToWorld(ref builder, pluginArgs);
                characterCameraPlugin.InjectToWorld(ref builder, pluginArgs);
                globalWorldSystems = builder.Finish();
                globalWorldSystems.Initialize();

                // Same as MainSceneLoader.RestoreInputs: enable all input maps except FreeCamera/EmoteWheel, and UI
                //if (inputInitSuccess)
                {
                    var inputBlock = new ECSInputBlock(globalWorldEcs);
                    inputBlock.EnableAll(InputMapComponent.Kind.FREE_CAMERA, InputMapComponent.Kind.EMOTE_WHEEL);
                    DCLInput.Instance.UI.Enable();
                }

                // Use Cinemachine Brain's output camera as MainCamera (CharacterCameraPlugin set it up)
                if (exposedCameraData.CameraEntityProxy.Object != default && globalWorldEcs.Has<CameraComponent>(exposedCameraData.CameraEntityProxy.Object))
                {
                    MainCamera = globalWorldEcs.Get<CameraComponent>(exposedCameraData.CameraEntityProxy.Object).Camera;
                }

                // Initialize mocked dependencies with proper realm data
                MockedDependencies dependencies = CreateMockedDependencies(realmData, exposedPlayerTransform, exposedCameraData, characterContainer);

                // Create scene factory
                SceneFactory sceneFactory = CreateSceneFactory(dependencies);

                if (!string.IsNullOrEmpty(worldName) && serverAbout != null)
                {
                    try { sceneFacade = await LoadSceneFromWorldAsync(worldName, serverAbout, sceneFactory, realmData, destroyCancellationToken); }
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

                    if (appArgs.TryGetValue(SCENE_URL_ARG, out string? urlArg) && !string.IsNullOrEmpty(urlArg)) { sceneUrl = urlArg; }
                    else { sceneUrl = GetSceneUrlFromQueryString() ?? sceneUrl; }

                    // Debug.Log($"[WebGLSceneBootstrapper] Loading scene from URL: {sceneUrl}");

                    if (!Uri.TryCreate(sceneUrl, UriKind.Absolute, out Uri? validatedUri)) { throw new ArgumentException($"Invalid scene URL format: {sceneUrl}"); }

                    // Debug.Log($"[WebGLSceneBootstrapper] URL validated. Scheme: {validatedUri.Scheme}, Host: {validatedUri.Host}");

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

                // Debug.Log($"[WebGLSceneBootstrapper] Initializing Scene Facade");
                sceneFacade.Initialize();

                // Set fake profile on the scene's player entity so scene logic/SDK can read profile (e.g. display name)
                Entity scenePlayerEntity = sceneFacade.PersistentEntities.Player;
                World sceneWorld = sceneFacade.EcsExecutor.World;
                Profile fakeProfile = Profile.NewRandomProfile("webgl-player");
                if (sceneWorld.Has<Profile>(scenePlayerEntity))
                    sceneWorld.Set(scenePlayerEntity, fakeProfile);
                else
                    sceneWorld.Add(scenePlayerEntity, fakeProfile);

                // Apply main.crdt (and other static CRDT) to ECS so entities (e.g. GltfContainers) exist before first tick
                sceneFacade.ApplyStaticMessagesIfAny();

                // CRITICAL: Set the scene state to Running so ECS systems will update
                // Debug.Log($"[WebGLSceneBootstrapper] Setting scene state to Running");
                sceneFacade.SceneStateProvider.SetRunning(new SceneEngineStartInfo(DateTime.Now, 0));

                // Debug.Log($"[WebGLSceneBootstrapper] Starting Scene");
                await sceneFacade.StartScene();

                isInitialized = true;

                // Debug.Log($"[WebGLSceneBootstrapper] Scene loaded and started successfully");

                // Set initial player/camera position from base parcel so first frame has correct pose
                SetInitialPlayerAndCameraPosition(currentSceneBaseParcel);

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

        private async UniTask<ISceneFacade> LoadSceneFromWorldAsync(
            string worldName,
            ServerAbout about,
            SceneFactory sceneFactory,
            IRealmData realmData,
            CancellationToken ct)
        {
            // World name should already be normalized by InitializeAsync

            // Step 1: Extract scene URN (about was already fetched in InitializeAsync)
            if (about.configurations.scenesUrn == null || about.configurations.scenesUrn.Count == 0) { throw new InvalidOperationException($"World '{worldName}' has no scenes defined"); }

            string sceneUrn = about.configurations.scenesUrn[0];

            // Debug.Log($"[WebGLSceneBootstrapper] Scene URN: {sceneUrn}");

            // Step 2: Parse URN to get IpfsPath
            IpfsPath ipfsPath = IpfsHelper.ParseUrn(sceneUrn);

            // Debug.Log($"[WebGLSceneBootstrapper] Parsed IpfsPath: {ipfsPath}");

            // Step 3: Construct content base URL and scene definition URL
            // For worlds, content is served from worlds-content-server, NOT from about.content.publicUrl
            var contentBaseUrl = URLDomain.FromString(WORLDS_CONTENT_URL);
            URLAddress sceneDefUrl = ipfsPath.GetUrl(contentBaseUrl);

            // Debug.Log($"[WebGLSceneBootstrapper] Content base URL: {contentBaseUrl.Value}");
            // Debug.Log($"[WebGLSceneBootstrapper] Fetching scene definition from: {sceneDefUrl.Value}");

            // Step 4: Fetch scene definition
            IWebRequestController webRequestController = CreateWebRequestController();

            SceneEntityDefinition sceneDefinition = await webRequestController
                                                         .GetAsync(new CommonArguments(sceneDefUrl), ct, ReportCategory.SCENE_LOADING)
                                                         .CreateFromJson<SceneEntityDefinition>(WRJsonParser.Newtonsoft);

            sceneDefinition.id ??= ipfsPath.EntityId;

            // Manifest fallback: worlds scene definition often lacks assetBundleManifestVersion; fetch from AB CDN.
            // Worlds use platform-less manifest: {sceneId}.json. Mainnet uses {sceneId}_mac.json etc. Try platform-less first, then platform suffixes.
            if (sceneDefinition.assetBundleManifestVersion == null || sceneDefinition.assetBundleManifestVersion.IsEmpty())
            {
                URLDomain assetBundleBaseUrl = string.IsNullOrEmpty(realmData.Ipfs.AssetBundleRegistry.Value)
                    ? URLDomain.FromString("https://ab-cdn.decentraland.org/")
                    : realmData.Ipfs.AssetBundleRegistry;

                // "" => {sceneId}.json (worlds); "_webGL", "_mac", "_windows" => mainnet-style
                string[] pathSuffixesToTry = { "", PlatformUtils.GetCurrentPlatform(), "_mac", "_windows" };
                var urlBuilder = new URLBuilder();
                Exception? lastEx = null;

                foreach (string suffix in pathSuffixesToTry)
                {
                    urlBuilder.Clear();

                    urlBuilder.AppendDomain(assetBundleBaseUrl)
                              .AppendSubDirectory(URLSubdirectory.FromString("manifest"))
                              .AppendPath(URLPath.FromString($"{sceneDefinition.id}{suffix}.json"));

                    URLAddress manifestUrl = urlBuilder.Build();

                    try
                    {
                        SceneAbDto dto = await webRequestController
                                              .GetAsync(new CommonArguments(manifestUrl), ct, ReportCategory.SCENE_LOADING)
                                              .CreateFromJson<SceneAbDto>(WRJsonParser.Newtonsoft);

                        if (!string.IsNullOrEmpty(dto.Version) && !string.IsNullOrEmpty(dto.Date))
                        {
                            sceneDefinition.assetBundleManifestVersion = AssetBundleManifestVersion.CreateManualManifest(dto.Version, dto.Date, dto.Version, dto.Date, dto.Version, dto.Date);

                            if (suffix != "")
                                Debug.Log($"[WebGLSceneBootstrapper] Manifest fallback: used {suffix}.json (version={dto.Version}) for scene {sceneDefinition.id}");

                            break;
                        }
                    }
                    catch (Exception ex) { lastEx = ex; }
                }

                if (sceneDefinition.assetBundleManifestVersion == null || sceneDefinition.assetBundleManifestVersion.IsEmpty())
                {
                    sceneDefinition.assetBundleManifestVersion = AssetBundleManifestVersion.CreateFailed();
                    Debug.LogWarning($"[WebGLSceneBootstrapper] Manifest fetch failed for scene {sceneDefinition.id} after trying: {sceneDefinition.id}.json, then _webGL, _mac, _windows. Last error: {lastEx?.Message ?? "empty version/date"}");
                }

                sceneDefinition.assetBundleManifestVersion.InjectContent(sceneDefinition.id, sceneDefinition.content ?? Array.Empty<ContentDefinition>());
            }

            // Step 5: Create SceneData with proper content
            var sceneContent = new SceneHashedContent(sceneDefinition.content, contentBaseUrl);

            // Load main.crdt (initial entities/components including GltfContainers) so ECS can spawn models
            byte[] mainCrdtBytes = Array.Empty<byte>();

            if (sceneContent.TryGetContentUrl("main.crdt", out URLAddress mainCrdtUrl))
                mainCrdtBytes = await webRequestController.GetAsync(new CommonArguments(mainCrdtUrl), ct, ReportCategory.SCENE_LOADING).GetDataCopyAsync();

            StaticSceneMessages staticMessages = mainCrdtBytes.Length > 0 ? new StaticSceneMessages(mainCrdtBytes) : StaticSceneMessages.EMPTY;

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
                staticMessages,
                new ISceneData.FakeInitialSceneState()
            );

            // Debug.Log($"[WebGLSceneBootstrapper] SceneData created. Base: {baseParcel}, Parcels: {parcels.Count}");

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

            // Debug.Log($"[WebGLSceneBootstrapper] Scene facade created successfully");
            return facade;
        }

        /// <summary>
        /// Preloads shader bundles from StreamingAssets so they're available when GLTF and wearable Asset Bundles load.
        /// Must run before any asset bundles that reference these shaders (e.g. wearables reference DCL_Toon).
        /// </summary>
        private async UniTask PreloadShaderBundlesAsync()
        {
            string[] shaderBundles =
            {
                "dcl/scene_ignore",
                "dcl/universal render pipeline/lit_ignore",
                "dcl/toon_ignore",
            };
            string streamingAssetsPath = Application.streamingAssetsPath;

            foreach (string bundleName in shaderBundles)
            {
                string bundlePath = $"{streamingAssetsPath}/AssetBundles/{bundleName}";

                try
                {
                    Debug.Log($"[WebGLSceneBootstrapper] Loading shader bundle: {bundlePath}");

                    // Use UnityWebRequest for WebGL compatibility
                    using var request = UnityEngine.Networking.UnityWebRequestAssetBundle.GetAssetBundle(bundlePath);
                    await request.SendWebRequest();

                    if (request.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
                    {
                        AssetBundle bundle = UnityEngine.Networking.DownloadHandlerAssetBundle.GetContent(request);
                        if (bundle != null)
                        {
                            // Load all shaders from the bundle to make them available
                            var shaders = bundle.LoadAllAssets<Shader>();
                            Debug.Log($"[WebGLSceneBootstrapper] Loaded shader bundle '{bundleName}' with {shaders.Length} shaders");

                            foreach (var shader in shaders)
                            {
                                Debug.Log($"[WebGLSceneBootstrapper]   - Shader: {shader.name}");
                            }

                            // Also load shader variant collections if any
                            var variantCollections = bundle.LoadAllAssets<ShaderVariantCollection>();
                            foreach (var collection in variantCollections)
                            {
                                Debug.Log($"[WebGLSceneBootstrapper]   - Warming up ShaderVariantCollection: {collection.name} ({collection.variantCount} variants)");
                                collection.WarmUp();
                            }

                            // Keep bundle loaded - don't unload it
                        }
                        else
                        {
                            Debug.LogWarning($"[WebGLSceneBootstrapper] Shader bundle '{bundleName}' loaded but content is null");
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"[WebGLSceneBootstrapper] Failed to load shader bundle '{bundleName}': {request.error}");
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[WebGLSceneBootstrapper] Exception loading shader bundle '{bundleName}': {e.Message}");
                }
            }
        }

        private void SetInitialPlayerAndCameraPosition(Vector2Int? baseParcel)
        {
            if (exposedPlayerTransform == null || exposedCameraData == null)
                return;

            float centerX = baseParcel.HasValue ? (baseParcel.Value.x * 16) + 8 : 8f;
            float centerZ = baseParcel.HasValue ? (baseParcel.Value.y * 16) + 8 : 8f;

            var sceneCenter = new Vector3(centerX, 1f, centerZ);
            var cameraPosition = new Vector3(centerX, 5f, centerZ - 13f);
            Quaternion lookRotation = Quaternion.LookRotation(sceneCenter - cameraPosition);

            exposedPlayerTransform.Position.Value = cameraPosition;
            exposedPlayerTransform.Rotation.Value = lookRotation;
            exposedCameraData.WorldPosition.Value = cameraPosition;
            exposedCameraData.WorldRotation.Value = lookRotation;
        }

        private void PositionCameraAtSceneOrigin(Vector2Int? baseParcel = null)
        {

            Camera mainCamera = MainCamera;

            if (mainCamera == null)
            {
                Debug.LogWarning("[WebGLSceneBootstrapper] No main camera found to reposition");
                return;
            }

            // Use base parcel coordinates if provided, otherwise default to origin
            float centerX = baseParcel.HasValue ? (baseParcel.Value.x * 16) + 8 : 8f;
            float centerZ = baseParcel.HasValue ? (baseParcel.Value.y * 16) + 8 : 8f;

            var sceneCenter = new Vector3(centerX, 1f, centerZ);
            var cameraPosition = new Vector3(centerX, 5f, centerZ - 13f); // Slightly elevated and back from center

            mainCamera.transform.position = cameraPosition;
            mainCamera.transform.LookAt(sceneCenter);

            Debug.Log($"[WebGLSceneBootstrapper] Repositioned camera to {cameraPosition}, looking at {sceneCenter}");

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
#endif
}
