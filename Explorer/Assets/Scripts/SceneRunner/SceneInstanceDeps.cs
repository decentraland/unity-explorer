using Arch.Core;
using CommunicationData.URLHelpers;
using CRDT;
using CRDT.Deserializer;
using CRDT.Memory;
using CRDT.Protocol;
using CRDT.Serializer;
using CrdtEcsBridge.Components;
using CrdtEcsBridge.ComponentWriter;
using CrdtEcsBridge.ECSToCRDTWriter;
using CrdtEcsBridge.JsModulesImplementation;
using CrdtEcsBridge.JsModulesImplementation.Communications;
using CrdtEcsBridge.OutgoingMessages;
using CrdtEcsBridge.PoolsProviders;
using CrdtEcsBridge.RestrictedActions;
using CrdtEcsBridge.UpdateGate;
using CrdtEcsBridge.WorldSynchronizer;
using DCL.Interaction.Utility;
using DCL.PluginSystem.World.Dependencies;
using DCL.Time;
using DCL.Utilities.Extensions;
using ECS;
using ECS.Prioritization.Components;
using MVC;
using SceneRunner.ECSWorld;
using SceneRunner.Scene;
using SceneRunner.Scene.ExceptionsHandling;
using SceneRuntime;
using SceneRuntime.Apis.Modules;
using SceneRuntime.Apis.Modules.CommunicationsControllerApi;
using SceneRuntime.Apis.Modules.EngineApi;
using SceneRuntime.Apis.Modules.FetchApi;
using SceneRuntime.Apis.Modules.RestrictedActionsApi;
using SceneRuntime.Apis.Modules.Runtime;
using SceneRuntime.Apis.Modules.SceneApi;
using System;
using System.Collections.Generic;
using Utility.Multithreading;

namespace SceneRunner
{
    /// <summary>
    ///     Dependencies that are unique for each instance of the scene,
    ///     this class itself contains the first stage of dependencies
    /// </summary>
    internal class SceneInstanceDeps : IDisposable
    {
        public readonly CRDTProtocol CRDTProtocol;
        public readonly IInstancePoolsProvider PoolsProvider;
        public readonly ICRDTMemoryAllocator CRDTMemoryAllocator;
        public readonly IOutgoingCRDTMessagesProvider OutgoingCRDTMessagesProvider;
        public readonly IEntityCollidersSceneCache EntityCollidersCache;
        public readonly ISceneStateProvider SceneStateProvider;
        public readonly ISceneExceptionsHandler ExceptionsHandler;
        public readonly ECSWorldFacade ECSWorldFacade;

        public readonly ICRDTWorldSynchronizer CRDTWorldSynchronizer;
        public readonly URLAddress SceneCodeUrl;
        public readonly SceneEcsExecutor EcsExecutor;
        private readonly ISceneData sceneData;

        private readonly MutexSync ecsMutexSync;
        private readonly ICRDTDeserializer crdtDeserializer;
        private readonly IECSToCRDTWriter ecsToCRDTWriter;
        private readonly ISystemGroupsUpdateGate systemGroupThrottler;
        private readonly IWorldTimeProvider worldTimeProvider;
        private readonly ECSWorldInstanceSharedDependencies ecsWorldSharedDependencies;

        private readonly Dictionary<CRDTEntity, Entity> entitiesMap = new (1000, CRDTEntityComparer.INSTANCE);

        public SceneInstanceDeps(ISDKComponentsRegistry sdkComponentsRegistry, IEntityCollidersGlobalCache entityCollidersGlobalCache,
            ISceneData sceneData, IPartitionComponent partitionProvider,
            IECSWorldFactory ecsWorldFactory, ISceneEntityFactory entityFactory)
        {
            this.sceneData = sceneData;
            ecsMutexSync = new MutexSync();
            CRDTProtocol = new CRDTProtocol();
            worldTimeProvider = new WorldTimeProvider();
            SceneStateProvider = new SceneStateProvider();
            systemGroupThrottler = new SystemGroupsUpdateGate();

            PoolsProvider = InstancePoolsProvider.Create().EnsureNotNull();
            CRDTMemoryAllocator = CRDTPooledMemoryAllocator.Create().EnsureNotNull();
            crdtDeserializer = new CRDTDeserializer(CRDTMemoryAllocator);
            OutgoingCRDTMessagesProvider = new OutgoingCRDTMessagesProvider(sdkComponentsRegistry, CRDTProtocol, CRDTMemoryAllocator);
            ecsToCRDTWriter = new ECSToCRDTWriter(OutgoingCRDTMessagesProvider);
            EntityCollidersCache = EntityCollidersSceneCache.Create(entityCollidersGlobalCache);
            ExceptionsHandler = SceneExceptionsHandler.Create(SceneStateProvider, sceneData.SceneShortInfo, CRDTProtocol).EnsureNotNull();

            /* Pass dependencies here if they are needed by the systems */
            ecsWorldSharedDependencies = new ECSWorldInstanceSharedDependencies(sceneData, partitionProvider, ecsToCRDTWriter, entitiesMap,
                ExceptionsHandler, EntityCollidersCache, SceneStateProvider, ecsMutexSync, worldTimeProvider);

            ECSWorldFacade = ecsWorldFactory.CreateWorld(new ECSWorldFactoryArgs(ecsWorldSharedDependencies, systemGroupThrottler, sceneData));
            ECSWorldFacade.Initialize();
            CRDTWorldSynchronizer = new CRDTWorldSynchronizer(ECSWorldFacade.EcsWorld, sdkComponentsRegistry, entityFactory, entitiesMap);

            EcsExecutor = new SceneEcsExecutor(ECSWorldFacade.EcsWorld, ecsMutexSync);
            entityCollidersGlobalCache.AddSceneInfo(EntityCollidersCache, EcsExecutor);

            if (sceneData.IsSdk7()) // Create an instance of Scene Runtime on the thread pool
                sceneData.TryGetMainScriptUrl(out SceneCodeUrl);
            else
                SceneCodeUrl = URLAddress.FromString("https://renderer-artifacts.decentraland.org/sdk7-adaption-layer/main/index.js");
        }

        public void Dispose()
        {
            // The order can make a difference here
            ECSWorldFacade.Dispose();
            CRDTProtocol.Dispose();
            OutgoingCRDTMessagesProvider.Dispose();
            CRDTWorldSynchronizer.Dispose();
            PoolsProvider.Dispose();
            CRDTMemoryAllocator.Dispose();

            systemGroupThrottler.Dispose();
            EntityCollidersCache.Dispose();
            worldTimeProvider.Dispose();
            ecsMutexSync.Dispose();
            ExceptionsHandler.Dispose();
        }

        internal class WithRuntimeAndEngineAPI : IDisposable
        {
            public readonly IEngineApi EngineAPI;
            public readonly IRestrictedActionsAPI RestrictedActionsAPI;
            public readonly IRuntime RuntimeImplementation;
            public readonly ISceneApi SceneApiImplementation;
            public readonly IWebSocketApi WebSocketAipImplementation = new WebSocketApiImplementation();
            public readonly ICommunicationsControllerAPI CommunicationsControllerAPI;
            public readonly ISimpleFetchApi SimpleFetchApi = new LogSimpleFetchApi(new SimpleFetchApiImplementation());

            private readonly SceneInstanceDeps deps;
            private readonly ISceneRuntime runtime;

            public WithRuntimeAndEngineAPI
            (SceneInstanceDeps sceneInstanceDeps, SceneRuntimeImpl sceneRuntime, ISharedPoolsProvider sharedPoolsProvider, ICRDTSerializer crdtSerializer, IMVCManager mvcManager,
                IGlobalWorldActions globalWorldActions, IRealmData realmData, ICommunicationControllerHub messagePipesHub)
            {
                deps = sceneInstanceDeps;
                runtime = sceneRuntime;

                EngineAPI = new EngineAPIImplementation(
                    sharedPoolsProvider,
                    deps.PoolsProvider,
                    deps.CRDTProtocol,
                    deps.crdtDeserializer,
                    crdtSerializer,
                    deps.CRDTWorldSynchronizer,
                    deps.OutgoingCRDTMessagesProvider,
                    deps.systemGroupThrottler,
                    deps.ExceptionsHandler,
                    deps.ecsMutexSync);

                RestrictedActionsAPI = new RestrictedActionsAPIImplementation(mvcManager, deps.ecsWorldSharedDependencies.SceneStateProvider, globalWorldActions, deps.sceneData);
                RuntimeImplementation = new RuntimeImplementation(sceneRuntime, deps.sceneData, deps.worldTimeProvider, realmData);
                SceneApiImplementation = new SceneApiImplementation(deps.sceneData);
                CommunicationsControllerAPI = new CommunicationsControllerAPIImplementation(deps.sceneData, messagePipesHub, sceneRuntime, deps.CRDTMemoryAllocator, deps.ecsWorldSharedDependencies.SceneStateProvider);
            }

            public void Dispose()
            {
                CommunicationsControllerAPI.Dispose();
                SceneApiImplementation.Dispose();
                RuntimeImplementation.Dispose();
                RestrictedActionsAPI.Dispose();
                EngineAPI.Dispose();

                runtime.Dispose();
                deps.Dispose();
            }
        }
    }
}
