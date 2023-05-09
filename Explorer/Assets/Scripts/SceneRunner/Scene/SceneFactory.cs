using CRDT.Deserializer;
using CRDT.Protocol;
using CRDT.Serializer;
using CrdtEcsBridge.Components;
using CrdtEcsBridge.Engine;
using CrdtEcsBridge.OutgoingMessages;
using CrdtEcsBridge.WorldSynchronizer;
using Cysharp.Threading.Tasks;
using SceneRunner.ECSWorld;
using SceneRuntime.Factory;
using System.Threading;

namespace SceneRunner.Scene
{
    public class SceneFactory : ISceneFactory
    {
        private readonly IECSWorldFactory ecsWorldFactory;
        private readonly SceneRuntimeFactory sceneRuntimeFactory;
        private readonly IEngineAPIPoolsProvider engineAPIPoolsProvider;
        private readonly ICRDTDeserializer crdtDeserializer;
        private readonly ICRDTSerializer crdtSerializer;
        private readonly ISDKComponentsRegistry sdkComponentsRegistry;
        private readonly IEntityFactory entityFactory;

        public SceneFactory(
            IECSWorldFactory ecsWorldFactory,
            SceneRuntimeFactory sceneRuntimeFactory,
            IEngineAPIPoolsProvider engineAPIPoolsProvider,
            ICRDTDeserializer crdtDeserializer,
            ICRDTSerializer crdtSerializer,
            ISDKComponentsRegistry sdkComponentsRegistry,
            IEntityFactory entityFactory)
        {
            this.ecsWorldFactory = ecsWorldFactory;
            this.sceneRuntimeFactory = sceneRuntimeFactory;
            this.engineAPIPoolsProvider = engineAPIPoolsProvider;
            this.crdtDeserializer = crdtDeserializer;
            this.crdtSerializer = crdtSerializer;
            this.sdkComponentsRegistry = sdkComponentsRegistry;
            this.entityFactory = entityFactory;
        }

        public async UniTask<ISceneFacade> CreateScene(string jsCodeUrl, CancellationToken ct)
        {
            // Per scene instance dependencies
            var crdtProtocol = new CRDTProtocol();
            var outgoingCrtdMessagesProvider = new OutgoingCRTDMessagesProvider();

            var ecsWorldFacade = ecsWorldFactory.CreateWorld( /* Pass dependencies here if they are needed by the systems */);
            ecsWorldFacade.Initialize();

            // Create an instance of Scene Runtime on the thread pool
            var sceneRuntime = await sceneRuntimeFactory.CreateByPath(jsCodeUrl, ct, SceneRuntimeFactory.InstantiationBehavior.SwitchToThreadPool);

            var crdtWorldSynchronizer = new CRDTWorldSynchronizer(ecsWorldFacade.EcsWorld, sdkComponentsRegistry, entityFactory);

            var engineAPI = new EngineAPIImplementation(
                engineAPIPoolsProvider,
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
                crdtWorldSynchronizer);
        }
    }
}
