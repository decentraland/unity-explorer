using Arch.Core;
using CRDT.Deserializer;
using CRDT.Serializer;
using CrdtEcsBridge.Components;
using CrdtEcsBridge.PoolsProviders;
using Cysharp.Threading.Tasks;
using NSubstitute;
using NUnit.Framework;
using SceneRunner.ECSWorld;
using SceneRunner.Scene;
using SceneRunner.SceneRunner.Tests.TestUtils;
using SceneRuntime.Factory;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace SceneRunner.Tests
{
    [TestFixture]
    public class SceneFactoryShould
    {
        private SceneRuntimeFactory sceneRuntimeFactory;
        private IECSWorldFactory ecsWorldFactory;
        private ISharedPoolsProvider sharedPoolsProvider;
        private ICRDTDeserializer crdtDeserializer;
        private ICRDTSerializer crdtSerializer;
        private ISDKComponentsRegistry componentsRegistry;
        private SceneFactory sceneFactory;

        //private ISceneFacade sceneFacade;

        private string path;

        [SetUp]
        public void SetUp()
        {
            path = $"file://{Application.dataPath + "/../TestResources/Scenes/Cube/cube.js"}";

            var ecsWorldFacade = TestSystemsWorld.Create();

            sceneRuntimeFactory = new SceneRuntimeFactory();

            ecsWorldFactory = Substitute.For<IECSWorldFactory>();
            ecsWorldFactory.CreateWorld().Returns(ecsWorldFacade);

            sharedPoolsProvider = Substitute.For<ISharedPoolsProvider>();
            crdtSerializer = Substitute.For<ICRDTSerializer>();
            componentsRegistry = Substitute.For<ISDKComponentsRegistry>();

            sceneFactory = new SceneFactory(ecsWorldFactory, sceneRuntimeFactory, sharedPoolsProvider, crdtSerializer, componentsRegistry, new EntityFactory());
        }

        [Test]
        public async Task CreateSceneFacadeForTestScene()
        {
            ISceneFacade sceneFacade = await sceneFactory.CreateScene(path, CancellationToken.None);
            var sceneFacadeImpl = (SceneFacade)sceneFacade;

            try
            {
                Assert.IsNotNull(sceneFacade);

                Assert.IsNotNull(sceneFacadeImpl.crdtProtocol);
                Assert.IsNotNull(sceneFacadeImpl.runtimeInstance);
                Assert.IsNotNull(sceneFacadeImpl.outgoingCrtdMessagesProvider);
                Assert.IsNotNull(sceneFacadeImpl.crdtWorldSynchronizer);
                Assert.IsNotNull(sceneFacadeImpl.instancePoolsProvider);
                Assert.IsNotNull(sceneFacadeImpl.crdtMemoryAllocator);

                Assert.AreNotEqual(default(World), sceneFacadeImpl.ecsWorldFacade.EcsWorld);
            }
            finally
            {
                await UniTask.SwitchToMainThread();
                sceneFacade.Dispose();
            }
        }

        [Test]
        public async Task ReturnToTheThreadPool()
        {
            var threadId = Thread.CurrentThread.ManagedThreadId;
            ISceneFacade sceneFacade = await sceneFactory.CreateScene(path, CancellationToken.None);

            try { Assert.AreNotEqual(threadId, Thread.CurrentThread.ManagedThreadId); }
            finally
            {
                await UniTask.SwitchToMainThread();
                sceneFacade.Dispose();
            }
        }
    }
}
