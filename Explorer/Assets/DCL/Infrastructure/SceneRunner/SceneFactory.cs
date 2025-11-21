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
using Global.AppArgs;
using Microsoft.ClearScript;
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
        private readonly IAppArgs appArgs;

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
            ISystemClipboard systemClipboard,
            DecentralandEnvironment dclEnvironment,
            IAppArgs appArgs
        )
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
            this.appArgs = appArgs;
        }

        public async UniTask<ISceneFacade> CreateSceneFromFileAsync(string jsCodeUrl, IPartitionComponent partitionProvider, CancellationToken ct, string id = "")
        {
            int lastSlash = jsCodeUrl.LastIndexOf("/", StringComparison.Ordinal);
            string mainScenePath = jsCodeUrl[(lastSlash + 1)..];
            var baseUrl = URLDomain.FromString(jsCodeUrl[..(lastSlash + 1)]);

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
            var deps = new SceneInstanceDependencies(sdkComponentsRegistry, entityCollidersGlobalCache, sceneData, permissionsProvider, partitionProvider, ecsWorldFactory, entityFactory);

            // Try to create scene runtime
            SceneRuntimeImpl sceneRuntime;

            try { sceneRuntime = await sceneRuntimeFactory.CreateByPathAsync(deps.SceneCodeUrl, deps.PoolsProvider, sceneData.SceneShortInfo, ct, SceneRuntimeFactory.InstantiationBehavior.SwitchToThreadPool); }
            catch (Exception e)
            {
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
                var sdkCommsControllerAPI = new SDKMessageBusCommsAPIImplementation(sceneData, messagePipesHub, sceneRuntime, appArgs);
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

            try
            {
                sceneRuntime.ExecuteSceneJson();
            }
            catch (Exception e)
            {
                await ReportExceptionAsync(e, runtimeDeps, deps.ExceptionsHandler);
                throw;
            }

            if (sceneData.IsPortableExperience())
            {
                return new PortableExperienceSceneFacade(
                    sceneData,
                    runtimeDeps
                );
            }

            return new SceneFacade(
                sceneData,
                runtimeDeps
            );
        }

        private static async Task ReportExceptionAsync<T>(Exception e, T deps, ISceneExceptionsHandler exceptionsHandler) where T : IDisposable
        {
            // ScriptEngineException.ErrorDetails is ignored through the logging process which is vital in the reporting information
            if (e is ScriptEngineException scriptEngineException)
                exceptionsHandler.OnJavaScriptException(new ScriptEngineException(scriptEngineException.ErrorDetails));

            await UniTask.SwitchToMainThread(PlayerLoopTiming.Initialization);
            deps.Dispose();
        }
    }
}
