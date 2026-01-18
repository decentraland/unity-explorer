using CommunicationData.URLHelpers;
using CRDT.Serializer;
using CrdtEcsBridge.Components;
using CrdtEcsBridge.JsModulesImplementation;
using CrdtEcsBridge.JsModulesImplementation.Communications;
using CrdtEcsBridge.JsModulesImplementation.Communications.SDKMessageBus;
using CrdtEcsBridge.PoolsProviders;
using CrdtEcsBridge.RestrictedActions;
using Cysharp.Threading.Tasks;
using DCL.Interaction.Utility;
using DCL.Ipfs;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.Multiplayer.Profiles.Poses;
using DCL.Profiles;
using DCL.SkyBox;
using DCL.Web3;
using DCL.Web3.Identities;
using DCL.WebRequests;
using ECS;
using ECS.Prioritization.Components;
using MVC;
using PortableExperiences.Controller;
using SceneRunner.ECSWorld;
using SceneRunner.Scene;
using SceneRunner.Scene.ExceptionsHandling;
using SceneRuntime;
using SceneRuntime.Apis.Modules.EngineApi.SDKObservableEvents;
using SceneRuntime.Factory;
using SceneRuntime.ScenePermissions;
using System;
using System.Threading;
using System.Threading.Tasks;
using DCL.Clipboard;
using UnityEngine;
using UnityEngine.Networking;
using Utility;
using Utility.Multithreading;

namespace SceneRunner
{
    public class SceneFactory : ISceneFactory
    {
        private const bool ENABLE_SDK_OBSERVABLES = true;

        private readonly ICRDTSerializer crdtSerializer;
        private readonly IECSWorldFactory ecsWorldFactory;
        private readonly ISceneEntityFactory entityFactory;
        private readonly IEntityCollidersGlobalCache entityCollidersGlobalCache;
        private readonly IEthereumApi ethereumApi;
        private readonly IProfileRepository profileRepository;
        private readonly IWeb3IdentityCache identityCache;
        private readonly IDecentralandUrlsSource decentralandUrlsSource;
        private readonly IWebRequestController webRequestController;
        private readonly IRoomHub roomHub;
        private readonly SceneRuntimeFactory sceneRuntimeFactory;
        private readonly ISDKComponentsRegistry sdkComponentsRegistry;
        private readonly ISharedPoolsProvider sharedPoolsProvider;
        private readonly IMVCManager mvcManager;
        private readonly IRealmData? realmData;
        private readonly IPortableExperiencesController portableExperiencesController;
        private readonly SkyboxSettingsAsset skyboxSettings;
        private readonly ISceneCommunicationPipe messagePipesHub;
        private readonly IRemoteMetadata remoteMetadata;
        private readonly DecentralandEnvironment dclEnvironment;
        private readonly ISystemClipboard systemClipboard;

        private IGlobalWorldActions globalWorldActions = null!;

        public SceneFactory(
            IECSWorldFactory ecsWorldFactory,
            SceneRuntimeFactory sceneRuntimeFactory,
            ISharedPoolsProvider sharedPoolsProvider,
            ICRDTSerializer crdtSerializer,
            ISDKComponentsRegistry sdkComponentsRegistry,
            ISceneEntityFactory entityFactory,
            IEntityCollidersGlobalCache entityCollidersGlobalCache,
            IEthereumApi ethereumApi,
            IMVCManager mvcManager,
            IProfileRepository profileRepository,
            IWeb3IdentityCache identityCache,
            IDecentralandUrlsSource decentralandUrlsSource,
            IWebRequestController webRequestController,
            IRoomHub roomHub,
            IRealmData? realmData,
            IPortableExperiencesController portableExperiencesController,
            SkyboxSettingsAsset skyboxSettings,
            ISceneCommunicationPipe messagePipesHub,
            IRemoteMetadata remoteMetadata,
            DecentralandEnvironment dclEnvironment,
            ISystemClipboard systemClipboard)
        {
            this.ecsWorldFactory = ecsWorldFactory;
            this.sceneRuntimeFactory = sceneRuntimeFactory;
            this.sharedPoolsProvider = sharedPoolsProvider;
            this.crdtSerializer = crdtSerializer;
            this.sdkComponentsRegistry = sdkComponentsRegistry;
            this.entityFactory = entityFactory;
            this.entityCollidersGlobalCache = entityCollidersGlobalCache;
            this.ethereumApi = ethereumApi;
            this.mvcManager = mvcManager;
            this.profileRepository = profileRepository;
            this.identityCache = identityCache;
            this.decentralandUrlsSource = decentralandUrlsSource;
            this.webRequestController = webRequestController;
            this.roomHub = roomHub;
            this.systemClipboard = systemClipboard;
            this.realmData = realmData;
            this.portableExperiencesController = portableExperiencesController;
            this.skyboxSettings = skyboxSettings;
            this.messagePipesHub = messagePipesHub;
            this.remoteMetadata = remoteMetadata;
            this.dclEnvironment = dclEnvironment;
        }

        public async UniTask<ISceneFacade> CreateSceneFromFileAsync(string jsCodeUrl, IPartitionComponent partitionProvider, CancellationToken ct, string id = "")
        {
            // Extract base URL and main file path
            // For WebGL StreamingAssets, we want the scene root as the base, not the file's directory
            // e.g., http://.../cube-wave-16x16/bin/game.js -> base = .../cube-wave-16x16/, main = bin/game.js
            int lastSlash = jsCodeUrl.LastIndexOf("/", StringComparison.Ordinal);
            string fileName = jsCodeUrl[(lastSlash + 1)..]; // "game.js"
            string baseUrlString = jsCodeUrl[..(lastSlash + 1)]; // ".../bin/"
            
            // Check if there's a directory structure before the filename in the URL
            // Look for patterns like ".../Scenes/cube-wave-16x16/bin/game.js"
            // We want to detect if "bin/" is a directory before the file
            string mainScenePath = fileName;
            
            // Try to detect scene directory structure: look for common patterns
            // If baseUrl ends with something like ".../bin/", we might want the scene root instead
            // But we need to be careful - only do this if we can reliably detect the scene root
            // For now, let's check if the path before the file looks like a subdirectory
            int secondLastSlash = jsCodeUrl.LastIndexOf("/", lastSlash - 1);
            if (secondLastSlash > 0)
            {
                string pathBeforeFile = jsCodeUrl[(secondLastSlash + 1)..lastSlash]; // e.g., "bin"
                string pathBeforeThat = jsCodeUrl.Substring(0, secondLastSlash + 1); // e.g., ".../cube-wave-16x16/"
                
                // If the path before the file is a common subdirectory name (like "bin"), use scene root
                if (pathBeforeFile.Equals("bin", StringComparison.OrdinalIgnoreCase) ||
                    pathBeforeFile.Equals("src", StringComparison.OrdinalIgnoreCase))
                {
                    baseUrlString = pathBeforeThat;
                    mainScenePath = $"{pathBeforeFile}/{fileName}"; // "bin/game.js"
                }
            }
            
            // Validate the base URL before creating URLDomain
            if (!Uri.TryCreate(baseUrlString, UriKind.Absolute, out Uri? validatedBaseUri))
            {
                throw new ArgumentException($"Invalid base URL format: {baseUrlString} (extracted from: {jsCodeUrl})");
            }
            
            UnityEngine.Debug.Log($"[SceneFactory] CreateSceneFromFileAsync - Base URL: {baseUrlString}, Main path: {mainScenePath}");
            
            var baseUrl = URLDomain.FromString(baseUrlString);

            var sceneDefinition = new SceneEntityDefinition(
                id,
                new SceneMetadata
                {
                    main = mainScenePath,
                    runtimeVersion = "7",
                }
            );

            var sceneData = new SceneData(new SceneNonHashedContent(baseUrl), sceneDefinition, Vector2Int.zero,
                ParcelMathHelper.UNDEFINED_SCENE_GEOMETRY, Array.Empty<Vector2Int>(), StaticSceneMessages.EMPTY, new ISceneData.FakeInitialSceneState());

            return await CreateSceneAsync(sceneData, new AllowEverythingJsApiPermissionsProvider(), partitionProvider, ct);
        }

        public async UniTask<ISceneFacade> CreateSceneFromStreamableDirectoryAsync(string directoryName, IPartitionComponent partitionProvider, CancellationToken ct)
        {
            const string SCENE_JSON_FILE_NAME = "scene.json";

            var fullPath = URLDomain.FromString($"file://{Application.dataPath}/Scenes/TestJsScenes/{directoryName}/");

            string rawSceneJsonPath = fullPath.Value + SCENE_JSON_FILE_NAME;

            using var request = UnityWebRequest.Get(rawSceneJsonPath);
            await request.SendWebRequest().WithCancellation(ct);

            SceneMetadata sceneMetadata = JsonUtility.FromJson<SceneMetadata>(request.downloadHandler.text);

            var sceneDefinition = new SceneEntityDefinition(directoryName, sceneMetadata);

            var sceneData = new SceneData(new SceneNonHashedContent(fullPath), sceneDefinition,
                Vector2Int.zero, ParcelMathHelper.UNDEFINED_SCENE_GEOMETRY, Array.Empty<Vector2Int>(), StaticSceneMessages.EMPTY, new ISceneData.FakeInitialSceneState());

            return await CreateSceneAsync(sceneData, new AllowEverythingJsApiPermissionsProvider(), partitionProvider, ct);
        }

        public UniTask<ISceneFacade> CreateSceneFromSceneDefinition(ISceneData sceneData, IJsApiPermissionsProvider permissionsProvider, IPartitionComponent partitionProvider, CancellationToken ct) =>
            CreateSceneAsync(sceneData, permissionsProvider, partitionProvider, ct);

        public void SetGlobalWorldActions(IGlobalWorldActions actions)
        {
            globalWorldActions = actions;
        }

        private async UniTask<ISceneFacade> CreateSceneAsync(ISceneData sceneData, IJsApiPermissionsProvider permissionsProvider, IPartitionComponent partitionProvider, CancellationToken ct)
        {
            UnityEngine.Debug.Log($"[SceneFactory] CreateSceneAsync - Starting scene creation for: {sceneData.SceneShortInfo.Name}");
            
            var deps = new SceneInstanceDependencies(sdkComponentsRegistry, entityCollidersGlobalCache, sceneData, permissionsProvider, partitionProvider, ecsWorldFactory, entityFactory);

            // Try to create scene runtime
            ISceneRuntime sceneRuntime;

            UnityEngine.Debug.Log($"[SceneFactory] CreateSceneAsync - Calling CreateByPathAsync with URL: {deps.SceneCodeUrl.Value}");
            try 
            { 
                sceneRuntime = await sceneRuntimeFactory.CreateByPathAsync(deps.SceneCodeUrl, deps.PoolsProvider, sceneData.SceneShortInfo, ct, SceneRuntimeFactory.InstantiationBehavior.SWITCH_TO_THREAD_POOL);
                UnityEngine.Debug.Log($"[SceneFactory] CreateSceneAsync - CreateByPathAsync completed successfully");
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"[SceneFactory] CreateSceneAsync - CreateByPathAsync failed: {e.GetType().Name}: {e.Message}");
                await ReportExceptionAsync(e, deps, deps.ExceptionsHandler);
                throw;
            }

            if (ct.IsCancellationRequested)
            {
                await UniTask.SwitchToMainThread(PlayerLoopTiming.Initialization);
                deps.Dispose();
                sceneRuntime?.Dispose();
                throw new OperationCanceledException();
            }

            SceneInstanceDependencies.WithRuntimeAndJsAPIBase runtimeDeps;

            var engineAPIMutexOwner = new MultiThreadSync.Owner(nameof(EngineAPIImplementation));
            var ethereumApiImpl = new RestrictedEthereumApi(ethereumApi, permissionsProvider);

            if (ENABLE_SDK_OBSERVABLES)
            {
                var sdkCommsControllerAPI = new SDKMessageBusCommsAPIImplementation(sceneData, messagePipesHub, sceneRuntime);
                sceneRuntime.RegisterSDKMessageBusCommsApi(sdkCommsControllerAPI);

                runtimeDeps = new SceneInstanceDependencies.WithRuntimeJsAndSDKObservablesEngineAPI(deps, sceneRuntime,
                    sharedPoolsProvider, crdtSerializer, mvcManager, globalWorldActions, realmData!, messagePipesHub,
                    webRequestController, skyboxSettings, engineAPIMutexOwner, systemClipboard);

                sceneRuntime.RegisterAll(
                    (ISDKObservableEventsEngineApi)runtimeDeps.EngineAPI,
                    sdkCommsControllerAPI,
                    deps.ExceptionsHandler,
                    roomHub,
                    profileRepository,
                    runtimeDeps.SceneApiImplementation,
                    webRequestController,
                    runtimeDeps.RestrictedActionsAPI,
                    runtimeDeps.RuntimeImplementation,
                    ethereumApiImpl,
                    runtimeDeps.WebSocketAipImplementation,
                    identityCache,
                    dclEnvironment,
                    runtimeDeps.CommunicationsControllerAPI,
                    deps.PoolsProvider,
                    runtimeDeps.SimpleFetchApi,
                    sceneData,
                    realmData!,
                    portableExperiencesController,
                    remoteMetadata
                );
            }
            else
            {
                runtimeDeps = new SceneInstanceDependencies.WithRuntimeAndJsAPI(deps, sceneRuntime, sharedPoolsProvider,
                    crdtSerializer, mvcManager, globalWorldActions, realmData!, messagePipesHub, webRequestController,
                    skyboxSettings, engineAPIMutexOwner, systemClipboard);

                sceneRuntime.RegisterAll(
                    runtimeDeps.EngineAPI,
                    deps.ExceptionsHandler,
                    roomHub,
                    profileRepository,
                    runtimeDeps.SceneApiImplementation,
                    webRequestController,
                    runtimeDeps.RestrictedActionsAPI,
                    dclEnvironment,
                    runtimeDeps.RuntimeImplementation,
                    ethereumApiImpl,
                    runtimeDeps.WebSocketAipImplementation,
                    identityCache,
                    runtimeDeps.CommunicationsControllerAPI,
                    deps.PoolsProvider,
                    runtimeDeps.SimpleFetchApi,
                    sceneData,
                    realmData!,
                    portableExperiencesController,
                    remoteMetadata
                );
            }

            UnityEngine.Debug.Log($"[SceneFactory] CreateSceneAsync - Executing scene JSON");
            try
            {
                sceneRuntime.ExecuteSceneJson();
                UnityEngine.Debug.Log($"[SceneFactory] CreateSceneAsync - Scene JSON executed successfully");
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"[SceneFactory] CreateSceneAsync - ExecuteSceneJson failed: {e.GetType().Name}: {e.Message}");
                await ReportExceptionAsync(e, runtimeDeps, deps.ExceptionsHandler);
                throw;
            }

            UnityEngine.Debug.Log($"[SceneFactory] CreateSceneAsync - Creating SceneFacade");
            if (sceneData.IsPortableExperience())
            {
                UnityEngine.Debug.Log($"[SceneFactory] CreateSceneAsync - Creating PortableExperienceSceneFacade");
                return new PortableExperienceSceneFacade(
                    sceneData,
                    runtimeDeps
                );
            }

            UnityEngine.Debug.Log($"[SceneFactory] CreateSceneAsync - Creating regular SceneFacade");
            return new SceneFacade(
                sceneData,
                runtimeDeps
            );
        }

        private static async Task ReportExceptionAsync<T>(Exception e, T deps, ISceneExceptionsHandler exceptionsHandler) where T : IDisposable
        {
            // JavaScriptExecutionException.ErrorDetails is ignored through the logging process which is vital in the reporting information
            if (e is JavaScriptExecutionException javascriptExecutionException)
                exceptionsHandler.OnJavaScriptException(new JavaScriptExecutionException(javascriptExecutionException.ErrorDetails));

            await UniTask.SwitchToMainThread(PlayerLoopTiming.Initialization);
            deps.Dispose();
        }
    }
}
