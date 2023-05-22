using Arch.Core;
using CRDT;
using CRDT.Deserializer;
using CRDT.Memory;
using CRDT.Protocol;
using CRDT.Serializer;
using CrdtEcsBridge.Components;
using CrdtEcsBridge.Engine;
using CrdtEcsBridge.OutgoingMessages;
using CrdtEcsBridge.PoolsProviders;
using CrdtEcsBridge.WorldSynchronizer;
using Cysharp.Threading.Tasks;
using SceneRunner.ECSWorld;
using SceneRuntime;
using SceneRuntime.Factory;
using System.Collections.Generic;
using System.Threading;

namespace SceneRunner.Scene
{
    public class SceneFactory : ISceneFactory
    {
        private readonly IECSWorldFactory ecsWorldFactory;
        private readonly SceneRuntimeFactory sceneRuntimeFactory;
        private readonly ISharedPoolsProvider sharedPoolsProvider;
        private readonly ICRDTSerializer crdtSerializer;
        private readonly ISDKComponentsRegistry sdkComponentsRegistry;
        private readonly IEntityFactory entityFactory;

        public SceneFactory(
            IECSWorldFactory ecsWorldFactory,
            SceneRuntimeFactory sceneRuntimeFactory,
            ISharedPoolsProvider sharedPoolsProvider,
            ICRDTSerializer crdtSerializer,
            ISDKComponentsRegistry sdkComponentsRegistry,
            IEntityFactory entityFactory)
        {
            this.ecsWorldFactory = ecsWorldFactory;
            this.sceneRuntimeFactory = sceneRuntimeFactory;
            this.sharedPoolsProvider = sharedPoolsProvider;
            this.crdtSerializer = crdtSerializer;
            this.sdkComponentsRegistry = sdkComponentsRegistry;
            this.entityFactory = entityFactory;
        }

        public async UniTask<ISceneFacade> CreateScene(string jsCodeUrl, CancellationToken ct)
        {
            var entitiesMap = new Dictionary<CRDTEntity, Entity>(1000, CRDTEntityComparer.INSTANCE);

            // Per scene instance dependencies
            var crdtProtocol = new CRDTProtocol();
            var outgoingCrtdMessagesProvider = new OutgoingCRTDMessagesProvider();
            var instancePoolsProvider = InstancePoolsProvider.Create();
            var crdtMemoryAllocator = CRDTPooledMemoryAllocator.Create();
            var crdtDeserializer = new CRDTDeserializer(crdtMemoryAllocator);

            ECSWorldFacade ecsWorldFacade = ecsWorldFactory.CreateWorld(entitiesMap /* Pass dependencies here if they are needed by the systems */);
            ecsWorldFacade.Initialize();

            // Create an instance of Scene Runtime on the thread pool
            SceneRuntimeImpl sceneRuntime = await sceneRuntimeFactory.CreateByPath(jsCodeUrl, instancePoolsProvider, ct, SceneRuntimeFactory.InstantiationBehavior.SwitchToThreadPool);

            var crdtWorldSynchronizer = new CRDTWorldSynchronizer(ecsWorldFacade.EcsWorld, sdkComponentsRegistry, entityFactory, entitiesMap);

            var engineAPI = new EngineAPIImplementation(
                sharedPoolsProvider, instancePoolsProvider,
                crdtProtocol,
                crdtDeserializer,
                crdtSerializer,
                crdtWorldSynchronizer,
                outgoingCrtdMessagesProvider);

            sceneRuntime.RegisterEngineApi(engineAPI);

            return new SceneFacade(
                sceneRuntime,
                ecsWorldFacade,
                crdtProtocol,
                outgoingCrtdMessagesProvider,
                crdtWorldSynchronizer,
                instancePoolsProvider,
                crdtMemoryAllocator);
        }
    }
}
