using CommunicationData.URLHelpers;
using CRDT.Serializer;
using CrdtEcsBridge.Components;
using CrdtEcsBridge.JsModulesImplementation.Communications;
using CrdtEcsBridge.PoolsProviders;
using CrdtEcsBridge.RestrictedActions;
using Cysharp.Threading.Tasks;
using DCL.Interaction.Utility;
using DCL.Ipfs;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.Profiles;
using DCL.Web3;
using DCL.Web3.Identities;
using DCL.WebRequests;
using ECS;
using ECS.Prioritization.Components;
using Microsoft.ClearScript;
using MVC;
using SceneRunner.ECSWorld;
using SceneRunner.Scene;
using SceneRuntime;
using SceneRuntime.Factory;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.Networking;
using Utility;

namespace SceneRunner
{
    public class SceneFactory : ISceneFactory
    {
        private readonly ICRDTSerializer crdtSerializer;
        private readonly IECSWorldFactory ecsWorldFactory;
        private readonly ISceneEntityFactory entityFactory;
        private readonly IEntityCollidersGlobalCache entityCollidersGlobalCache;
        private readonly IEthereumApi ethereumApi;
        private readonly IProfileRepository profileRepository;
        private readonly IWeb3IdentityCache identityCache;
        private readonly IWebRequestController webRequestController;
        private readonly IRoomHub roomHub;
        private readonly SceneRuntimeFactory sceneRuntimeFactory;
        private readonly ISDKComponentsRegistry sdkComponentsRegistry;
        private readonly ISharedPoolsProvider sharedPoolsProvider;
        private readonly IMVCManager mvcManager;
        private readonly IRealmData? realmData;
        private readonly ICommunicationControllerHub messagePipesHub;

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
            IWebRequestController webRequestController,
            IRoomHub roomHub,
            IRealmData? realmData,
            ICommunicationControllerHub messagePipesHub)
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
            this.webRequestController = webRequestController;
            this.roomHub = roomHub;
            this.realmData = realmData;
            this.messagePipesHub = messagePipesHub;
        }

        public async UniTask<ISceneFacade> CreateSceneFromFileAsync(string jsCodeUrl, IPartitionComponent partitionProvider, CancellationToken ct)
        {
            int lastSlash = jsCodeUrl.LastIndexOf("/", StringComparison.Ordinal);
            string mainScenePath = jsCodeUrl[(lastSlash + 1)..];
            var baseUrl = URLDomain.FromString(jsCodeUrl[..(lastSlash + 1)]);

            var sceneDefinition = new SceneEntityDefinition(
                string.Empty,
                new SceneMetadata
                {
                    main = mainScenePath,
                    runtimeVersion = "7",
                }
            );

            var sceneData = new SceneData(new SceneNonHashedContent(baseUrl), sceneDefinition, SceneAssetBundleManifest.NULL, Vector2Int.zero,
                ParcelMathHelper.UNDEFINED_SCENE_GEOMETRY, Array.Empty<Vector2Int>(), StaticSceneMessages.EMPTY);

            return await CreateSceneAsync(sceneData, partitionProvider, ct);
        }

        public async UniTask<ISceneFacade> CreateSceneFromStreamableDirectoryAsync(string directoryName, IPartitionComponent partitionProvider, CancellationToken ct)
        {
            const string SCENE_JSON_FILE_NAME = "scene.json";

            var fullPath = URLDomain.FromString($"file://{Application.streamingAssetsPath}/Scenes/{directoryName}/");

            string rawSceneJsonPath = fullPath.Value + SCENE_JSON_FILE_NAME;

            using var request = UnityWebRequest.Get(rawSceneJsonPath);
            await request.SendWebRequest().WithCancellation(ct);

            SceneMetadata sceneMetadata = JsonUtility.FromJson<SceneMetadata>(request.downloadHandler.text);

            var sceneDefinition = new SceneEntityDefinition(directoryName, sceneMetadata);

            var sceneData = new SceneData(new SceneNonHashedContent(fullPath), sceneDefinition, SceneAssetBundleManifest.NULL,
                Vector2Int.zero, ParcelMathHelper.UNDEFINED_SCENE_GEOMETRY, Array.Empty<Vector2Int>(), StaticSceneMessages.EMPTY);

            return await CreateSceneAsync(sceneData, partitionProvider, ct);
        }

        public UniTask<ISceneFacade> CreateSceneFromSceneDefinition(ISceneData sceneData, IPartitionComponent partitionProvider, CancellationToken ct) =>
            CreateSceneAsync(sceneData, partitionProvider, ct);

        public void SetGlobalWorldActions(IGlobalWorldActions actions)
        {
            globalWorldActions = actions;
        }

        private async UniTask<ISceneFacade> CreateSceneAsync(ISceneData sceneData, IPartitionComponent partitionProvider, CancellationToken ct)
        {
            var deps = new SceneInstanceDeps(
                sdkComponentsRegistry,
                entityCollidersGlobalCache,
                sceneData,
                partitionProvider,
                ecsWorldFactory,
                entityFactory);

            SceneRuntimeImpl sceneRuntime;

            try { sceneRuntime = await sceneRuntimeFactory.CreateByPathAsync(deps.SceneCodeUrl, deps.PoolsProvider, sceneData.SceneShortInfo, ct, SceneRuntimeFactory.InstantiationBehavior.SwitchToThreadPool); }
            catch (Exception e)
            {
                // ScriptEngineException.ErrorDetails is ignored through the logging process which is vital in the reporting information
                if (e is ScriptEngineException scriptEngineException)
                    deps.ExceptionsHandler.OnJavaScriptException(new ScriptEngineException(scriptEngineException.ErrorDetails));

                await UniTask.SwitchToMainThread(PlayerLoopTiming.Initialization);
                deps.Dispose();

                throw;
            }

            if (ct.IsCancellationRequested)
            {
                await UniTask.SwitchToMainThread(PlayerLoopTiming.Initialization);
                deps.Dispose();
                sceneRuntime?.Dispose();

                throw new OperationCanceledException();
            }

            var runtimeDeps = new SceneInstanceDeps.WithRuntimeAndEngineAPI(deps, sceneRuntime, sharedPoolsProvider, crdtSerializer, mvcManager,
                globalWorldActions, realmData!, messagePipesHub);

            sceneRuntime.RegisterEngineApi(runtimeDeps.EngineAPI, deps.ExceptionsHandler);
            sceneRuntime.RegisterAll(
                deps.ExceptionsHandler,
                roomHub,
                profileRepository,
                runtimeDeps.SceneApiImplementation,
                webRequestController,
                runtimeDeps.RestrictedActionsAPI,
                runtimeDeps.RuntimeImplementation,
                ethereumApi,
                runtimeDeps.WebSocketAipImplementation,
                identityCache,
                runtimeDeps.CommunicationsControllerAPI,
                deps.PoolsProvider,
                runtimeDeps.SimpleFetchApi);
            sceneRuntime.ExecuteSceneJson();

            return new SceneFacade(
                sceneRuntime,
                deps.ECSWorldFacade,
                deps.CRDTProtocol,
                deps.OutgoingCRDTMessagesProvider,
                deps.CRDTWorldSynchronizer,
                deps.PoolsProvider,
                deps.CRDTMemoryAllocator,
                deps.ExceptionsHandler,
                deps.SceneStateProvider,
                deps.EntityCollidersCache,
                sceneData,
                deps.EcsExecutor);
        }
    }
}
