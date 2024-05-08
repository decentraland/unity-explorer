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
        private readonly ISceneData sceneData;

        public readonly Dictionary<CRDTEntity, Entity> EntitiesMap = new (1000, CRDTEntityComparer.INSTANCE);

        public readonly MutexSync EcsMutexSync;
        public readonly CRDTProtocol CRDTProtocol;
        public readonly IInstancePoolsProvider poolsProvider;
        public readonly ICRDTMemoryAllocator CRDTMemoryAllocator;
        public readonly ICRDTDeserializer CRDTDeserializer;
        public readonly IOutgoingCRDTMessagesProvider OutgoingCRDTMessagesProvider;
        public readonly IECSToCRDTWriter ECSToCRDTWriter;
        public readonly ISystemGroupsUpdateGate SystemGroupThrottler;
        public readonly IEntityCollidersSceneCache EntityCollidersCache;
        public readonly ISceneStateProvider SceneStateProvider;
        public readonly ISceneExceptionsHandler ExceptionsHandler;
        public readonly IWorldTimeProvider WorldTimeProvider;

        public readonly ECSWorldInstanceSharedDependencies ECSWorldSharedDependencies;
        public readonly ECSWorldFacade ECSWorldFacade;

        public readonly ICRDTWorldSynchronizer CRDTWorldSynchronizer;
        public readonly URLAddress SceneCodeUrl;
        public readonly SceneEcsExecutor EcsExecutor;

        public SceneInstanceDeps(ISDKComponentsRegistry sdkComponentsRegistry, IEntityCollidersGlobalCache entityCollidersGlobalCache,
            ISceneData sceneData, IPartitionComponent partitionProvider,
            IECSWorldFactory ecsWorldFactory, ISceneEntityFactory entityFactory)
        {
            this.sceneData = sceneData;
            EcsMutexSync = new MutexSync();
            CRDTProtocol = new CRDTProtocol();
            WorldTimeProvider = new WorldTimeProvider();
            SceneStateProvider = new SceneStateProvider();
            SystemGroupThrottler = new SystemGroupsUpdateGate();

            poolsProvider = InstancePoolsProvider.Create().EnsureNotNull();
            CRDTMemoryAllocator = CRDTPooledMemoryAllocator.Create().EnsureNotNull();
            CRDTDeserializer = new CRDTDeserializer(CRDTMemoryAllocator);
            OutgoingCRDTMessagesProvider = new OutgoingCRDTMessagesProvider(sdkComponentsRegistry, CRDTProtocol, CRDTMemoryAllocator);
            ECSToCRDTWriter = new ECSToCRDTWriter(OutgoingCRDTMessagesProvider);
            EntityCollidersCache = EntityCollidersSceneCache.Create(entityCollidersGlobalCache);
            ExceptionsHandler = SceneExceptionsHandler.Create(SceneStateProvider, sceneData.SceneShortInfo, CRDTProtocol).EnsureNotNull();

            /* Pass dependencies here if they are needed by the systems */
            ECSWorldSharedDependencies = new ECSWorldInstanceSharedDependencies(sceneData, partitionProvider, ECSToCRDTWriter, EntitiesMap,
                ExceptionsHandler, EntityCollidersCache, SceneStateProvider, EcsMutexSync, WorldTimeProvider);

            ECSWorldFacade = ecsWorldFactory.CreateWorld(new ECSWorldFactoryArgs(ECSWorldSharedDependencies, SystemGroupThrottler, sceneData));
            ECSWorldFacade.Initialize();
            CRDTWorldSynchronizer = new CRDTWorldSynchronizer(ECSWorldFacade.EcsWorld, sdkComponentsRegistry, entityFactory, EntitiesMap);

            EcsExecutor = new SceneEcsExecutor(ECSWorldFacade.EcsWorld, EcsMutexSync);
            entityCollidersGlobalCache.AddSceneInfo(EntityCollidersCache, EcsExecutor);

            if (!sceneData.IsSdk7())
                SceneCodeUrl = URLAddress.FromString("https://renderer-artifacts.decentraland.org/sdk7-adaption-layer/main/index.js");
            else // Create an instance of Scene Runtime on the thread pool
                sceneData.TryGetMainScriptUrl(out SceneCodeUrl);
        }

        public void Dispose()
        {
            // The order can make a difference here
            ECSWorldFacade.Dispose();
            CRDTProtocol.Dispose();
            OutgoingCRDTMessagesProvider.Dispose();
            CRDTWorldSynchronizer.Dispose();
            poolsProvider.Dispose();
            CRDTMemoryAllocator.Dispose();

            SystemGroupThrottler.Dispose();
            EntityCollidersCache.Dispose();
            WorldTimeProvider.Dispose();
            EcsMutexSync.Dispose();
            ExceptionsHandler.Dispose();
        }

        internal class WithRuntimeAndEngineAPI : IDisposable
        {
            private readonly SceneInstanceDeps deps;

            private readonly ISceneRuntime runtime;
            public readonly IEngineApi EngineAPI;
            public readonly IRestrictedActionsAPI RestrictedActionsAPI;
            public readonly IRuntime RuntimeImplementation;
            public readonly ISceneApi SceneApiImplementation;
            public readonly IWebSocketApi WebSocketAipImplementation = new WebSocketApiImplementation();
            public readonly ICommunicationsControllerAPI CommunicationsControllerAPI;
            public readonly ISimpleFetchApi SimpleFetchApi = new LogSimpleFetchApi(new SimpleFetchApiImplementation());

            public WithRuntimeAndEngineAPI
            (SceneInstanceDeps sceneInstanceDeps, SceneRuntimeImpl sceneRuntime, ISharedPoolsProvider sharedPoolsProvider, ICRDTSerializer crdtSerializer, IMVCManager mvcManager,
                IGlobalWorldActions globalWorldActions, IRealmData realmData, ICommunicationControllerHub messagePipesHub)
            {
                deps = sceneInstanceDeps;
                runtime = sceneRuntime;

                EngineAPI = new EngineAPIImplementation(
                    sharedPoolsProvider,
                    deps.poolsProvider,
                    deps.CRDTProtocol,
                    deps.CRDTDeserializer,
                    crdtSerializer,
                    deps.CRDTWorldSynchronizer,
                    deps.OutgoingCRDTMessagesProvider,
                    deps.SystemGroupThrottler,
                    deps.ExceptionsHandler,
                    deps.EcsMutexSync);

                RestrictedActionsAPI = new RestrictedActionsAPIImplementation(mvcManager, deps.ECSWorldSharedDependencies.SceneStateProvider, globalWorldActions, deps.sceneData);
                RuntimeImplementation = new RuntimeImplementation(sceneRuntime, deps.sceneData, deps.WorldTimeProvider, realmData);
                SceneApiImplementation = new SceneApiImplementation(deps.sceneData);
                CommunicationsControllerAPI = new CommunicationsControllerAPIImplementation(deps.sceneData, messagePipesHub, sceneRuntime, deps.CRDTMemoryAllocator, deps.ECSWorldSharedDependencies.SceneStateProvider);
            }

            public void Dispose()
            {
                runtime.Dispose();
                EngineAPI.Dispose();
                deps.Dispose();
            }
        }
    }
}
