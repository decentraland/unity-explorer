using Arch.Core;
using CRDT.Deserializer;
using CRDT.Serializer;
using CrdtEcsBridge.Components;
using CrdtEcsBridge.Engine;
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

        private ISceneFacade sceneFacade;

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
            crdtDeserializer = Substitute.For<ICRDTDeserializer>();
            crdtSerializer = Substitute.For<ICRDTSerializer>();
            componentsRegistry = Substitute.For<ISDKComponentsRegistry>();

            sceneFactory = new SceneFactory(ecsWorldFactory, sceneRuntimeFactory, sharedPoolsProvider, crdtDeserializer, crdtSerializer, componentsRegistry, new EntityFactory());
        }

        [TearDown]
        public void TearDown()
        {
            sceneFacade?.Dispose();
        }

        [Test]
        public async Task CreateSceneFacadeForTestScene()
        {
            sceneFacade = await sceneFactory.CreateScene(path, CancellationToken.None);

            var sceneFacadeImpl = (SceneFacade)sceneFacade;

            Assert.IsNotNull(sceneFacade);

            Assert.IsNotNull(sceneFacadeImpl.crdtProtocol);
            Assert.IsNotNull(sceneFacadeImpl.runtimeInstance);
            Assert.IsNotNull(sceneFacadeImpl.outgoingCrtdMessagesProvider);
            Assert.IsNotNull(sceneFacadeImpl.crdtWorldSynchronizer);

            Assert.AreNotEqual(default(World), sceneFacadeImpl.ecsWorldFacade.EcsWorld);
        }

        [Test]
        public async Task ReturnToTheThreadPool()
        {
            var threadId = Thread.CurrentThread.ManagedThreadId;

            sceneFacade = await sceneFactory.CreateScene(path, CancellationToken.None);

            Assert.AreNotEqual(threadId, Thread.CurrentThread.ManagedThreadId);
        }
    }
}
