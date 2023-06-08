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
using Ipfs;
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
        private readonly ICRDTSerializer crdtSerializer;
        private readonly IECSWorldFactory ecsWorldFactory;
        private readonly IEntityFactory entityFactory;
        private readonly SceneRuntimeFactory sceneRuntimeFactory;
        private readonly ISDKComponentsRegistry sdkComponentsRegistry;
        private readonly ISharedPoolsProvider sharedPoolsProvider;

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
            var sceneDefinition = new IpfsTypes.SceneEntityDefinition();

            int lastSlash = jsCodeUrl.LastIndexOf("/", StringComparison.Ordinal);
            string mainScenePath = jsCodeUrl.Substring(lastSlash + 1);
            string baseUrl = jsCodeUrl.Substring(0, lastSlash + 1);

            sceneDefinition.metadata = new IpfsTypes.SceneMetadata
            {
                main = mainScenePath,
            };

            var sceneData = new SceneData(new IpfsRealm(baseUrl, baseUrl), sceneDefinition, false, SceneAssetBundleManifest.NULL);

            return await CreateScene(sceneData, ct);
        }

        public async UniTask<ISceneFacade> CreateSceneFromStreamableDirectory(string directoryName, CancellationToken ct)
        {
            const string SCENE_JSON_FILE_NAME = "scene.json";

            var fullPath = $"file://{Application.streamingAssetsPath}/Scenes/{directoryName}/";

            string rawSceneJsonPath = fullPath + SCENE_JSON_FILE_NAME;

            var request = UnityWebRequest.Get(rawSceneJsonPath);
            await request.SendWebRequest().WithCancellation(ct);

            IpfsTypes.SceneMetadata sceneMetadata = JsonUtility.FromJson<IpfsTypes.SceneMetadata>(request.downloadHandler.text);

            var sceneDefinition = new IpfsTypes.SceneEntityDefinition
            {
                id = directoryName,
                metadata = sceneMetadata,
            };

            var sceneData = new SceneData(new IpfsRealm(fullPath, fullPath), sceneDefinition, false, SceneAssetBundleManifest.NULL);

            return await CreateScene(sceneData, ct);
        }

        public async UniTask<ISceneFacade> CreateSceneFromSceneDefinition(IIpfsRealm ipfsRealm, IpfsTypes.SceneEntityDefinition sceneDefinition, SceneAssetBundleManifest abManifest, CancellationToken ct)
        {
            var sceneData = new SceneData(ipfsRealm, sceneDefinition, true, abManifest);

            return await CreateScene(sceneData, ct);
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
