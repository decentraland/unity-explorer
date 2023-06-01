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
using SceneRunner.Scene;
using SceneRuntime;
using SceneRuntime.Factory;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Networking;

namespace SceneRunner
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

        public UniTask<ISceneFacade> CreateScene(string jsCodeUrl, CancellationToken ct) =>
            throw new NotImplementedException();

        public async UniTask<ISceneFacade> CreateSceneFromStreamableDirectory(string directoryName, CancellationToken ct)
        {
            throw new NotImplementedException();
            /*const string SCENE_JSON_FILE_NAME = "scene.json";

            var fullPath = $"file://{Application.streamingAssetsPath}/Scenes/{directoryName}";

            string rawSceneJsonPath = fullPath + "/" + SCENE_JSON_FILE_NAME;

            var request = UnityWebRequest.Get(rawSceneJsonPath);
            await request.SendWebRequest().WithCancellation(ct);

            RawSceneJson rawScene = JsonUtility.FromJson<RawSceneJson>(request.downloadHandler.text);

            var contentProvider = new SceneData(new SceneData(fullPath, in rawScene), false);
            string jsCodeUrl = fullPath + "/" + rawScene.main;

            return await CreateScene(contentProvider, jsCodeUrl, ct);*/
        }

        public async UniTask<ISceneFacade> CreateSceneFromSceneDefinition(string contentBaseUrl, Ipfs.SceneEntityDefinition sceneDefinition, CancellationToken ct)
        {
            var contentProvider = new SceneData(contentBaseUrl, sceneDefinition, true);

            return await CreateScene(contentProvider, ct);
        }

        private async UniTask<ISceneFacade> CreateScene(ISceneData sceneData, CancellationToken ct)
        {
            var entitiesMap = new Dictionary<CRDTEntity, Entity>(1000, CRDTEntityComparer.INSTANCE);

            // Per scene instance dependencies
            var crdtProtocol = new CRDTProtocol();
            var outgoingCrtdMessagesProvider = new OutgoingCRTDMessagesProvider();
            var instancePoolsProvider = InstancePoolsProvider.Create();
            var crdtMemoryAllocator = CRDTPooledMemoryAllocator.Create();
            var crdtDeserializer = new CRDTDeserializer(crdtMemoryAllocator);

            /* Pass dependencies here if they are needed by the systems */
            var instanceDependencies = new ECSWorldInstanceSharedDependencies(sceneData, entitiesMap);

            ECSWorldFacade ecsWorldFacade = ecsWorldFactory.CreateWorld(in instanceDependencies);
            ecsWorldFacade.Initialize();

            // Create an instance of Scene Runtime on the thread pool
            sceneData.TryGetMainScriptUrl(out string sceneCodeUrl);
            SceneRuntimeImpl sceneRuntime = await sceneRuntimeFactory.CreateByPath(sceneCodeUrl, instancePoolsProvider, ct, SceneRuntimeFactory.InstantiationBehavior.SwitchToThreadPool);

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
