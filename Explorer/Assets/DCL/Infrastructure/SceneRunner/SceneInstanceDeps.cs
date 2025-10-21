﻿using Arch.Core;
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
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.PluginSystem.World.Dependencies;
using DCL.Time;
using DCL.Utilities.Extensions;
using DCL.WebRequests;
using ECS;
using ECS.Abstract;
using ECS.Prioritization.Components;
using Global.AppArgs;
using MVC;
using PortableExperiences.Controller;
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
        public readonly ICRDTProtocol CRDTProtocol;
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
        internal readonly ISystemGroupsUpdateGate systemGroupThrottler;
        private readonly ISystemsUpdateGate systemsUpdateGate;
        internal readonly IWorldTimeProvider worldTimeProvider;
        private readonly ISceneData sceneData;

        private readonly MultiThreadSync ecsMultiThreadSync;
        private readonly ICRDTDeserializer crdtDeserializer;
        private readonly IECSToCRDTWriter ecsToCRDTWriter;
        private readonly ECSWorldInstanceSharedDependencies ecsWorldSharedDependencies;

        private readonly Dictionary<CRDTEntity, Entity> entitiesMap = new (1000, CRDTEntityComparer.INSTANCE);

        /// <summary>
        ///     For Unit Test only
        /// </summary>
        internal SceneInstanceDependencies(
            ICRDTProtocol crdtProtocol,
            IInstancePoolsProvider poolsProvider,
            ICRDTMemoryAllocator crdtMemoryAllocator,
            IOutgoingCRDTMessagesProvider outgoingCRDTMessagesProvider,
            IEntityCollidersSceneCache entityCollidersCache,
            ISceneStateProvider sceneStateProvider,
            ISceneExceptionsHandler exceptionsHandler,
            ECSWorldFacade ecsWorldFacade,
            ICRDTWorldSynchronizer crdtWorldSynchronizer,
            URLAddress sceneCodeUrl,
            SceneEcsExecutor ecsExecutor,
            ISceneData sceneData,
            MultiThreadSync ecsMultiThreadSync,
            ICRDTDeserializer crdtDeserializer,
            IECSToCRDTWriter ecsToCRDTWriter,
            ISystemGroupsUpdateGate systemGroupThrottler,
            ISystemsUpdateGate systemsUpdateGate,
            IWorldTimeProvider worldTimeProvider,
            ECSWorldInstanceSharedDependencies ecsWorldSharedDependencies)
        {
            CRDTProtocol = crdtProtocol;
            PoolsProvider = poolsProvider;
            CRDTMemoryAllocator = crdtMemoryAllocator;
            OutgoingCRDTMessagesProvider = outgoingCRDTMessagesProvider;
            EntityCollidersCache = entityCollidersCache;
            SceneStateProvider = sceneStateProvider;
            ExceptionsHandler = exceptionsHandler;
            ECSWorldFacade = ecsWorldFacade;
            CRDTWorldSynchronizer = crdtWorldSynchronizer;
            SceneCodeUrl = sceneCodeUrl;
            EcsExecutor = ecsExecutor;
            this.sceneData = sceneData;
            this.ecsMultiThreadSync = ecsMultiThreadSync;
            this.crdtDeserializer = crdtDeserializer;
            this.ecsToCRDTWriter = ecsToCRDTWriter;
            this.systemGroupThrottler = systemGroupThrottler;
            this.systemsUpdateGate = systemsUpdateGate;
            this.worldTimeProvider = worldTimeProvider;
            this.ecsWorldSharedDependencies = ecsWorldSharedDependencies;
        }

        public SceneInstanceDependencies(
            IDecentralandUrlsSource decentralandUrlsSource,
            ISDKComponentsRegistry sdkComponentsRegistry,
            IEntityCollidersGlobalCache entityCollidersGlobalCache,
            ISceneData sceneData,
            IPartitionComponent partitionProvider,
            IECSWorldFactory ecsWorldFactory,
            ISceneEntityFactory entityFactory,
            IWebRequestController webRequestController)
        {
            this.sceneData = sceneData;
            ecsMultiThreadSync = new MultiThreadSync(sceneData.SceneShortInfo);
            CRDTProtocol = new CRDTProtocol();
            worldTimeProvider = new WorldTimeProvider(decentralandUrlsSource, webRequestController);
            SceneStateProvider = new SceneStateProvider();
            systemGroupThrottler = new SystemGroupsUpdateGate();
            systemsUpdateGate = new SystemsPriorityComponentsGate();
            PoolsProvider = InstancePoolsProvider.Create().EnsureNotNull();
            CRDTMemoryAllocator = CRDTPooledMemoryAllocator.Create().EnsureNotNull();
            crdtDeserializer = new CRDTDeserializer(CRDTMemoryAllocator);
            OutgoingCRDTMessagesProvider = new OutgoingCRDTMessagesProvider(sdkComponentsRegistry, CRDTProtocol, CRDTMemoryAllocator);
            ecsToCRDTWriter = new ECSToCRDTWriter(OutgoingCRDTMessagesProvider);
            EntityCollidersCache = EntityCollidersSceneCache.Create(entityCollidersGlobalCache);
            ExceptionsHandler = SceneExceptionsHandler.Create(SceneStateProvider, sceneData.SceneShortInfo).EnsureNotNull();
            var entityEventsBuilder = new EntityEventsBuilder();

            /* Pass dependencies here if they are needed by the systems */
            ecsWorldSharedDependencies = new ECSWorldInstanceSharedDependencies(sceneData, partitionProvider, ecsToCRDTWriter, entitiesMap,
                ExceptionsHandler, EntityCollidersCache, SceneStateProvider, entityEventsBuilder, ecsMultiThreadSync,
                worldTimeProvider, systemGroupThrottler, systemsUpdateGate);

            ECSWorldFacade = ecsWorldFactory.CreateWorld(new ECSWorldFactoryArgs(ecsWorldSharedDependencies, systemGroupThrottler, sceneData));
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
            systemsUpdateGate.Dispose();
            EntityCollidersCache.Dispose();
            worldTimeProvider.Dispose();
            ecsMultiThreadSync.Dispose();
            ExceptionsHandler.Dispose();
        }

        /// <summary>
        ///     The base class is for Observables
        /// </summary>
        public abstract class WithRuntimeAndJsAPIBase : IDisposable
        {
            public readonly IRestrictedActionsAPI RestrictedActionsAPI;
            public readonly IRuntime RuntimeImplementation;
            public readonly ISceneApi SceneApiImplementation;
            public readonly IWebSocketApi WebSocketAipImplementation;
            public readonly ICommunicationsControllerAPI CommunicationsControllerAPI;
            public readonly ISimpleFetchApi SimpleFetchApi;

            public readonly SceneInstanceDependencies SyncDeps;
            public readonly ISceneRuntime Runtime;
            public readonly IEngineApi EngineAPI;

            /// <summary>
            ///     For Unit Tests only
            /// </summary>
            protected internal WithRuntimeAndJsAPIBase(
                IEngineApi engineAPI,
                IRestrictedActionsAPI restrictedActionsAPI,
                IRuntime runtimeImplementation,
                ISceneApi sceneApiImplementation,
                IWebSocketApi webSocketApi,
                ISimpleFetchApi simpleFetchApi,
                ICommunicationsControllerAPI communicationsControllerAPI,
                SceneInstanceDependencies syncDeps,
                ISceneRuntime runtime)
            {
                EngineAPI = engineAPI;
                RestrictedActionsAPI = restrictedActionsAPI;
                RuntimeImplementation = runtimeImplementation;
                SceneApiImplementation = sceneApiImplementation;
                CommunicationsControllerAPI = communicationsControllerAPI;
                WebSocketAipImplementation = webSocketApi;
                SimpleFetchApi = simpleFetchApi;
                SyncDeps = syncDeps;
                Runtime = runtime;
            }

            protected WithRuntimeAndJsAPIBase(
                IEngineApi engineApi,
                SceneInstanceDependencies syncDeps,
                ISceneRuntime sceneRuntime,
                IJsOperations jsOperations,
                IMVCManager mvcManager,
                IGlobalWorldActions globalWorldActions,
                IRealmData realmData,
                ISceneCommunicationPipe messagePipesHub,
                IWebRequestController webRequestController,
                IAppArgs appArgs)
                : this(
                    engineApi,
                    new RestrictedActionsAPIImplementation(mvcManager, syncDeps.ecsWorldSharedDependencies.SceneStateProvider, globalWorldActions, syncDeps.sceneData),
                    new RuntimeImplementation(jsOperations, syncDeps.sceneData, syncDeps.worldTimeProvider, realmData, webRequestController),
                    new SceneApiImplementation(syncDeps.sceneData),
                    new ClientWebSocketApiImplementation(syncDeps.PoolsProvider, jsOperations),
                    new LogSimpleFetchApi(new SimpleFetchApiImplementation(syncDeps.sceneData.SceneShortInfo)),
                    new CommunicationsControllerAPIImplementation(syncDeps.sceneData, messagePipesHub, jsOperations, appArgs),
                    syncDeps,
                    sceneRuntime) { }

            public void Dispose()
            {
                // Runtime is responsible to dispose APIs

                Runtime.Dispose();
                SyncDeps.Dispose();
            }
        }

        internal class WithRuntimeAndJsAPI : WithRuntimeAndJsAPIBase
        {
            public WithRuntimeAndJsAPI
            (
                SceneInstanceDependencies syncDeps,
                SceneRuntimeImpl sceneRuntime,
                ISharedPoolsProvider sharedPoolsProvider,
                ICRDTSerializer crdtSerializer,
                IMVCManager mvcManager,
                IGlobalWorldActions globalWorldActions,
                IRealmData realmData,
                ISceneCommunicationPipe messagePipesHub,
                IWebRequestController webRequestController,
                MultiThreadSync.Owner syncOwner,
                IAppArgs appArgs
            )
                : base(
                    new EngineAPIImplementation(
                        sharedPoolsProvider,
                        syncDeps.PoolsProvider,
                        syncDeps.CRDTProtocol,
                        syncDeps.crdtDeserializer,
                        crdtSerializer,
                        syncDeps.CRDTWorldSynchronizer,
                        syncDeps.OutgoingCRDTMessagesProvider,
                        syncDeps.systemGroupThrottler,
                        syncDeps.ExceptionsHandler,
                        syncDeps.ecsMultiThreadSync,
                        syncOwner
                    ),
                    syncDeps,
                    sceneRuntime,
                    sceneRuntime,
                    mvcManager,
                    globalWorldActions,
                    realmData,
                    messagePipesHub,
                    webRequestController,
                    appArgs
                ) { }
        }

        internal class WithRuntimeJsAndSDKObservablesEngineAPI : WithRuntimeAndJsAPIBase
        {
            public WithRuntimeJsAndSDKObservablesEngineAPI
            (SceneInstanceDependencies syncDeps, SceneRuntimeImpl sceneRuntime, ISharedPoolsProvider sharedPoolsProvider, ICRDTSerializer crdtSerializer, IMVCManager mvcManager,
                IGlobalWorldActions globalWorldActions, IRealmData realmData, ISceneCommunicationPipe messagePipesHub,
                IWebRequestController webRequestController, MultiThreadSync.Owner syncOwner, IAppArgs appArgs)
                : base(
                    new SDKObservableEventsEngineAPIImplementation(
                        sharedPoolsProvider,
                        syncDeps.PoolsProvider,
                        syncDeps.CRDTProtocol,
                        syncDeps.crdtDeserializer,
                        crdtSerializer,
                        syncDeps.CRDTWorldSynchronizer,
                        syncDeps.OutgoingCRDTMessagesProvider,
                        syncDeps.systemGroupThrottler,
                        syncDeps.ExceptionsHandler,
                        syncDeps.ecsMultiThreadSync,
                        syncOwner
                    ),
                    syncDeps,
                    sceneRuntime,
                    sceneRuntime,
                    mvcManager,
                    globalWorldActions,
                    realmData,
                    messagePipesHub,
                    webRequestController,
                    appArgs
                ) { }
        }
    }
}
