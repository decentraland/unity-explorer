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
using ECS.Abstract;
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
    public class SceneInstanceDependencies : IDisposable
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

        public SceneInstanceDependencies(ISDKComponentsRegistry sdkComponentsRegistry, IEntityCollidersGlobalCache entityCollidersGlobalCache,
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
            var entityEventsBuilder = new EntityEventsBuilder();

            /* Pass dependencies here if they are needed by the systems */
            ecsWorldSharedDependencies = new ECSWorldInstanceSharedDependencies(sceneData, partitionProvider, ecsToCRDTWriter, entitiesMap,
                ExceptionsHandler, EntityCollidersCache, SceneStateProvider, entityEventsBuilder, ecsMutexSync, worldTimeProvider);

            ECSWorldFacade = ecsWorldFactory.CreateWorld(new ECSWorldFactoryArgs(ecsWorldSharedDependencies, systemGroupThrottler, sceneData));
            ECSWorldFacade.Initialize();
            CRDTWorldSynchronizer = new CRDTWorldSynchronizer(ECSWorldFacade.EcsWorld, sdkComponentsRegistry, entityFactory, entitiesMap);

            EcsExecutor = new SceneEcsExecutor(ECSWorldFacade.EcsWorld);
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
            public IEngineApi EngineAPI;
            public readonly IRestrictedActionsAPI RestrictedActionsAPI;
            public readonly IRuntime RuntimeImplementation;
            public readonly ISceneApi SceneApiImplementation;
            public readonly IWebSocketApi WebSocketAipImplementation = new WebSocketApiImplementation();
            public readonly ICommunicationsControllerAPI CommunicationsControllerAPI;
            public readonly ISimpleFetchApi SimpleFetchApi = new LogSimpleFetchApi(new SimpleFetchApiImplementation());

            protected SceneInstanceDependencies dependencies;
            protected ISceneRuntime runtime;

            public WithRuntimeAndEngineAPI
            (SceneInstanceDependencies sceneInstanceDependencies, SceneRuntimeImpl sceneRuntime, ISharedPoolsProvider sharedPoolsProvider, ICRDTSerializer crdtSerializer, IMVCManager mvcManager,
                IGlobalWorldActions globalWorldActions, IRealmData realmData, ICommunicationControllerHub messagePipesHub)
            {
                dependencies = sceneInstanceDependencies;
                runtime = sceneRuntime;

                EngineAPI = new EngineAPIImplementation(
                    sharedPoolsProvider,
                    dependencies.PoolsProvider,
                    dependencies.CRDTProtocol,
                    dependencies.crdtDeserializer,
                    crdtSerializer,
                    dependencies.CRDTWorldSynchronizer,
                    dependencies.OutgoingCRDTMessagesProvider,
                    dependencies.systemGroupThrottler,
                    dependencies.ExceptionsHandler,
                    dependencies.ecsMutexSync);

                RestrictedActionsAPI = new RestrictedActionsAPIImplementation(mvcManager, dependencies.ecsWorldSharedDependencies.SceneStateProvider, globalWorldActions, dependencies.sceneData);
                RuntimeImplementation = new RuntimeImplementation(sceneRuntime, dependencies.sceneData, dependencies.worldTimeProvider, realmData);
                SceneApiImplementation = new SceneApiImplementation(dependencies.sceneData);
                CommunicationsControllerAPI = new CommunicationsControllerAPIImplementation(dependencies.sceneData, messagePipesHub, sceneRuntime, dependencies.CRDTMemoryAllocator, dependencies.ecsWorldSharedDependencies.SceneStateProvider);
            }

            public void Dispose()
            {
                CommunicationsControllerAPI.Dispose();
                SceneApiImplementation.Dispose();
                RuntimeImplementation.Dispose();
                RestrictedActionsAPI.Dispose();
                EngineAPI.Dispose();

                runtime.Dispose();
                dependencies.Dispose();
            }
        }

        internal class WithRuntimeAndSDKObservablesEngineAPI : WithRuntimeAndEngineAPI
        {
            public WithRuntimeAndSDKObservablesEngineAPI
            (SceneInstanceDependencies sceneInstanceDependencies, SceneRuntimeImpl sceneRuntime, ISharedPoolsProvider sharedPoolsProvider, ICRDTSerializer crdtSerializer, IMVCManager mvcManager,
                IGlobalWorldActions globalWorldActions, IRealmData realmData, ICommunicationControllerHub messagePipesHub) : base(sceneInstanceDependencies, sceneRuntime, sharedPoolsProvider, crdtSerializer, mvcManager,
                globalWorldActions, realmData, messagePipesHub)
            {
                dependencies = sceneInstanceDependencies;
                runtime = sceneRuntime;

                EngineAPI = new SDKObservableEventsEngineAPIImplementation(
                    sharedPoolsProvider,
                    dependencies.PoolsProvider,
                    dependencies.CRDTProtocol,
                    dependencies.crdtDeserializer,
                    crdtSerializer,
                    dependencies.CRDTWorldSynchronizer,
                    dependencies.OutgoingCRDTMessagesProvider,
                    dependencies.systemGroupThrottler,
                    dependencies.ExceptionsHandler,
                    dependencies.ecsMutexSync);
            }
        }
    }
}
