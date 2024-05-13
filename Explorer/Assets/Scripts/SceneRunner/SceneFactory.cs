using Arch.Core;
using CommunicationData.URLHelpers;
using CRDT;
using CRDT.Deserializer;
using CRDT.Memory;
using CRDT.Protocol;
using CRDT.Serializer;
using CrdtEcsBridge.Components;
using CrdtEcsBridge.ComponentWriter;
using CrdtEcsBridge.JsModulesImplementation;
using CrdtEcsBridge.JsModulesImplementation.Communications;
using CrdtEcsBridge.JsModulesImplementation.Communications.SDKMessageBus;
using CrdtEcsBridge.OutgoingMessages;
using CrdtEcsBridge.PoolsProviders;
using CrdtEcsBridge.RestrictedActions;
using CrdtEcsBridge.UpdateGate;
using CrdtEcsBridge.WorldSynchronizer;
using Cysharp.Threading.Tasks;
using DCL.Interaction.Utility;
using DCL.Ipfs;
using DCL.Multiplayer.Connections.Messaging.Hubs;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.PluginSystem.World.Dependencies;
using DCL.Profiles;
using DCL.Time;
using DCL.Utilities.Extensions;
using DCL.Web3;
using DCL.Web3.Identities;
using DCL.WebRequests;
using ECS;
using ECS.Prioritization.Components;
using Microsoft.ClearScript;
using MVC;
using SceneRunner.ECSWorld;
using SceneRunner.Scene;
using SceneRunner.Scene.ExceptionsHandling;
using SceneRuntime;
using SceneRuntime.Apis.Modules.EngineApi;
using SceneRuntime.Apis.Modules.EngineApi.SDKObservableEvents;
using SceneRuntime.Apis.Modules.FetchApi;
using SceneRuntime.Factory;
using System;
using System.Collections.Generic;
using System.Threading;
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
            var entitiesMap = new Dictionary<CRDTEntity, Entity>(1000, CRDTEntityComparer.INSTANCE);

            // Per scene instance dependencies
            var ecsMutexSync = new MutexSync();
            var crdtProtocol = new CRDTProtocol();
            InstancePoolsProvider instancePoolsProvider = InstancePoolsProvider.Create().EnsureNotNull();
            CRDTPooledMemoryAllocator crdtMemoryAllocator = CRDTPooledMemoryAllocator.Create().EnsureNotNull();
            var crdtDeserializer = new CRDTDeserializer(crdtMemoryAllocator);
            var outgoingCRDTMessagesProvider = new OutgoingCRDTMessagesProvider(sdkComponentsRegistry, crdtProtocol, crdtMemoryAllocator);
            var ecsToCrdtWriter = new ECSToCRDTWriter(outgoingCRDTMessagesProvider);
            var systemGroupThrottler = new SystemGroupsUpdateGate();
            var entityCollidersCache = EntityCollidersSceneCache.Create(entityCollidersGlobalCache);
            var sceneStateProvider = new SceneStateProvider();
            SceneExceptionsHandler exceptionsHandler = SceneExceptionsHandler.Create(sceneStateProvider, sceneData.SceneShortInfo, crdtProtocol).EnsureNotNull();
            var worldTimeProvider = new WorldTimeProvider();

            /* Pass dependencies here if they are needed by the systems */
            var instanceDependencies = new ECSWorldInstanceSharedDependencies(sceneData, partitionProvider, ecsToCrdtWriter, entitiesMap, exceptionsHandler, entityCollidersCache, sceneStateProvider, ecsMutexSync, worldTimeProvider);

            ECSWorldFacade ecsWorldFacade = ecsWorldFactory.CreateWorld(new ECSWorldFactoryArgs(instanceDependencies, systemGroupThrottler, sceneData));
            ecsWorldFacade.Initialize();

            var ecsExecutor = new SceneEcsExecutor(ecsWorldFacade.EcsWorld, ecsMutexSync);
            entityCollidersGlobalCache.AddSceneInfo(entityCollidersCache, ecsExecutor);

            URLAddress sceneCodeUrl;

            if (!sceneData.IsSdk7())
                sceneCodeUrl = URLAddress.FromString("https://renderer-artifacts.decentraland.org/sdk7-adaption-layer/main/index.js");
            else
            {
                // Create an instance of Scene Runtime on the thread pool
                sceneData.TryGetMainScriptUrl(out sceneCodeUrl);
            }

            SceneRuntimeImpl sceneRuntime;

            try { sceneRuntime = await sceneRuntimeFactory.CreateByPathAsync(sceneCodeUrl, instancePoolsProvider, sceneData.SceneShortInfo, ct, SceneRuntimeFactory.InstantiationBehavior.SwitchToThreadPool); }
            catch (ScriptEngineException e)
            {
                // ScriptEngineException.ErrorDetails is ignored through the logging process which is vital in the reporting information
                exceptionsHandler.OnJavaScriptException(new ScriptEngineException(e.ErrorDetails));

                await UniTask.SwitchToMainThread(PlayerLoopTiming.Initialization);

                ecsWorldFacade.Dispose();
                crdtProtocol.Dispose();
                outgoingCRDTMessagesProvider.Dispose();
                instancePoolsProvider.Dispose();
                crdtMemoryAllocator.Dispose();
                exceptionsHandler.Dispose();
                entityCollidersCache.Dispose();

                throw;
            }

            ct.ThrowIfCancellationRequested();

            var crdtWorldSynchronizer = new CRDTWorldSynchronizer(ecsWorldFacade.EcsWorld, sdkComponentsRegistry, entityFactory, entitiesMap);
            var restrictedActionsAPI = new RestrictedActionsAPIImplementation(mvcManager, instanceDependencies.SceneStateProvider, globalWorldActions, sceneData);
            var runtimeImplementation = new RuntimeImplementation(sceneRuntime, sceneData, worldTimeProvider, realmData);
            var sceneApiImplementation = new SceneApiImplementation(sceneData);
            var webSocketAipImplementation = new WebSocketApiImplementation();
            var communicationsControllerAPI = new CommunicationsControllerAPIImplementation(sceneData, messagePipesHub, sceneRuntime, crdtMemoryAllocator, instanceDependencies.SceneStateProvider);
            var simpleFetchApi = new LogSimpleFetchApi(new SimpleFetchApiImplementation());

            IEngineApi engineAPI;
            if (ENABLE_SDK_OBSERVABLES)
            {
                engineAPI = new SDKObservableEventsEngineAPIImplementation(
                    sharedPoolsProvider, instancePoolsProvider,
                    crdtProtocol,
                    crdtDeserializer,
                    crdtSerializer,
                    crdtWorldSynchronizer,
                    outgoingCRDTMessagesProvider,
                    systemGroupThrottler,
                    exceptionsHandler,
                    ecsMutexSync,
                    crdtMemoryAllocator);
                var sdkCommsControllerAPI = new SDKMessageBusCommsAPIImplementation(sceneData, messagePipesHub, sceneRuntime, instanceDependencies.SceneStateProvider);

                sceneRuntime.RegisterSDKObservablesEngineApi((engineAPI as ISDKObservableEventsEngineApi)!, sdkCommsControllerAPI, exceptionsHandler);
                sceneRuntime.RegisterSDKMessageBusCommsApi(sdkCommsControllerAPI);
            }
            else
            {
                engineAPI = new EngineAPIImplementation(
                    sharedPoolsProvider, instancePoolsProvider,
                    crdtProtocol,
                    crdtDeserializer,
                    crdtSerializer,
                    crdtWorldSynchronizer,
                    outgoingCRDTMessagesProvider,
                    systemGroupThrottler,
                    exceptionsHandler,
                    ecsMutexSync);
                sceneRuntime.RegisterEngineApi(engineAPI, exceptionsHandler);
            }

            sceneRuntime.RegisterAll(
                exceptionsHandler,
                roomHub,
                profileRepository,
                sceneApiImplementation,
                webRequestController,
                restrictedActionsAPI,
                runtimeImplementation,
                ethereumApi,
                webSocketAipImplementation,
                identityCache,
                communicationsControllerAPI,
                instancePoolsProvider,
                simpleFetchApi);

            sceneRuntime.ExecuteSceneJson();

            return new SceneFacade(
                sceneRuntime,
                ecsWorldFacade,
                crdtProtocol,
                outgoingCRDTMessagesProvider,
                crdtWorldSynchronizer,
                instancePoolsProvider,
                crdtMemoryAllocator,
                exceptionsHandler,
                sceneStateProvider,
                entityCollidersCache,
                sceneData,
                ecsExecutor);
        }
    }
}
