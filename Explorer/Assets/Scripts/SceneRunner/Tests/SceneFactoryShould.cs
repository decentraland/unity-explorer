using Arch.Core;
using CRDT.Deserializer;
using CRDT.Serializer;
using CrdtEcsBridge.Components;
using CrdtEcsBridge.PoolsProviders;
using Cysharp.Threading.Tasks;
using DCL.Interaction.Utility;
using DCL.Profiles;
using DCL.Web3;
using DCL.Web3.Identities;
using ECS;
using ECS.Prioritization.Components;
using ECS.TestSuite;
using MVC;
using NSubstitute;
using NUnit.Framework;
using SceneRunner.ECSWorld;
using SceneRunner.Scene;
using SceneRunner.Tests.TestUtils;
using SceneRuntime.Factory;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace SceneRunner.Tests
{
    [TestFixture]
    public class SceneFactoryShould
    {
        [SetUp]
        public void SetUp()
        {
            path = $"file://{Application.dataPath + "/../TestResources/Scenes/Cube/cube.js"}";

            ECSWorldFacade ecsWorldFacade = TestSystemsWorld.Create();

            sceneRuntimeFactory = new SceneRuntimeFactory(TestWebRequestController.INSTANCE);

            ecsWorldFactory = Substitute.For<IECSWorldFactory>();
            ecsWorldFactory.CreateWorld(in Arg.Any<ECSWorldFactoryArgs>()).Returns(ecsWorldFacade);

            sharedPoolsProvider = Substitute.For<ISharedPoolsProvider>();
            crdtSerializer = Substitute.For<ICRDTSerializer>();
            componentsRegistry = Substitute.For<ISDKComponentsRegistry>();

            sceneFactory = new SceneFactory(ecsWorldFactory, sceneRuntimeFactory, sharedPoolsProvider, crdtSerializer, componentsRegistry,
                new SceneEntityFactory(), new EntityCollidersGlobalCache(), Substitute.For<IEthereumApi>(), Substitute.For<IMVCManager>(),
                Substitute.For<IProfileRepository>(), Substitute.For<IWeb3IdentityCache>(), Substitute.For<IRealmData>());
        }

        [TearDown]
        public void TearDown()
        {
            sceneFacade?.DisposeAsync().Forget();
        }

        private SceneRuntimeFactory sceneRuntimeFactory;
        private IECSWorldFactory ecsWorldFactory;
        private ISharedPoolsProvider sharedPoolsProvider;
        private ICRDTDeserializer crdtDeserializer;
        private ICRDTSerializer crdtSerializer;
        private ISDKComponentsRegistry componentsRegistry;
        private SceneFactory sceneFactory;

        private ISceneFacade sceneFacade;

        private string path;

        [Test]
        public async Task CreateSceneFacadeForTestScene()
        {
            sceneFacade = await sceneFactory.CreateSceneFromFileAsync(path, Substitute.For<IPartitionComponent>(), CancellationToken.None);

            var sceneFacadeImpl = (SceneFacade)sceneFacade;

            Assert.IsNotNull(sceneFacade);

            Assert.IsNotNull(sceneFacadeImpl.crdtProtocol);
            Assert.IsNotNull(sceneFacadeImpl.runtimeInstance);
            Assert.IsNotNull(sceneFacadeImpl.crdtWorldSynchronizer);
            Assert.IsNotNull(sceneFacadeImpl.instancePoolsProvider);
            Assert.IsNotNull(sceneFacadeImpl.crdtMemoryAllocator);
            Assert.IsNotNull(sceneFacadeImpl.sceneExceptionsHandler);

            Assert.AreNotEqual(default(World), sceneFacadeImpl.ecsWorldFacade.EcsWorld);
        }

        [Test]
        public async Task ReturnToTheThreadPool()
        {
            int threadId = Thread.CurrentThread.ManagedThreadId;

            sceneFacade = await sceneFactory.CreateSceneFromFileAsync(path, Substitute.For<IPartitionComponent>(), CancellationToken.None);

            Assert.AreNotEqual(threadId, Thread.CurrentThread.ManagedThreadId);
        }
    }
}
