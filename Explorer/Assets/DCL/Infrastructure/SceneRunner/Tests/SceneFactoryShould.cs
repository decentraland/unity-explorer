using Arch.Core;
using CRDT.Deserializer;
using CRDT.Serializer;
using CrdtEcsBridge.Components;
using CrdtEcsBridge.JsModulesImplementation.Communications;
using CrdtEcsBridge.PoolsProviders;
using DCL.AssetsProvision.CodeResolver;
using DCL.Interaction.Utility;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.Multiplayer.Profiles.Poses;
using DCL.Profiles;
using DCL.Web3;
using DCL.Web3.Identities;
using DCL.WebRequests;
using ECS;
using ECS.Prioritization.Components;
using ECS.TestSuite;
using MVC;
using NSubstitute;
using NUnit.Framework;
using PortableExperiences.Controller;
using SceneRunner.ECSWorld;
using SceneRunner.Scene;
using SceneRunner.Tests.TestUtils;
using SceneRuntime;
using SceneRuntime.Factory;
using SceneRuntime.Factory.WebSceneSource;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace SceneRunner.Tests
{
    [TestFixture(WebRequestsMode.UNITY)]
    [TestFixture(WebRequestsMode.HTTP2)]
    [TestFixture(WebRequestsMode.YET_ANOTHER)]
    public class SceneFactoryShould
    {
        [SetUp]
        public void SetUp()
        {
            path = $"file://{Application.dataPath + "/../TestResources/Scenes/Cube/cube.js"}";
            engineFactory = new V8EngineFactory();

            ECSWorldFacade ecsWorldFacade = TestSystemsWorld.Create();
            IWebRequestController webRequestController = TestWebRequestController.Create(webRequestsMode);

            sceneRuntimeFactory = new SceneRuntimeFactory(new IRealmData.Fake(), engineFactory, new WebJsSources(new JsCodeResolver(webRequestController)));

            ecsWorldFactory = Substitute.For<IECSWorldFactory>();
            ecsWorldFactory.CreateWorld(in Arg.Any<ECSWorldFactoryArgs>()).Returns(ecsWorldFacade);

            sharedPoolsProvider = Substitute.For<ISharedPoolsProvider>();
            crdtSerializer = Substitute.For<ICRDTSerializer>();
            componentsRegistry = Substitute.For<ISDKComponentsRegistry>();

            sceneFactory = new SceneFactory(
                ecsWorldFactory,
                sceneRuntimeFactory,
                sharedPoolsProvider,
                crdtSerializer,
                componentsRegistry,
                new SceneEntityFactory(),
                new EntityCollidersGlobalCache(),
                Substitute.For<IEthereumApi>(),
                Substitute.For<IMVCManager>(),
                Substitute.For<IProfileRepository>(),
                Substitute.For<IWeb3IdentityCache>(),
                Substitute.For<IDecentralandUrlsSource>(),
                IWebRequestController.UNITY,
                NullRoomHub.INSTANCE,
                Substitute.For<IRealmData>(),
                Substitute.For<IPortableExperiencesController>(),
                Substitute.For<ISceneCommunicationPipe>(),
                Substitute.For<IRemoteMetadata>());
        }

        [TearDown]
        public async Task TearDown()
        {
            TestWebRequestController.RestoreCache();

            if (sceneFacade != null)
                await sceneFacade.DisposeAsync();
        }

        private readonly WebRequestsMode webRequestsMode;

        private V8EngineFactory engineFactory;

        private SceneRuntimeFactory sceneRuntimeFactory;
        private IECSWorldFactory ecsWorldFactory;
        private ISharedPoolsProvider sharedPoolsProvider;
        private ICRDTDeserializer crdtDeserializer;
        private ICRDTSerializer crdtSerializer;
        private ISDKComponentsRegistry componentsRegistry;
        private SceneFactory sceneFactory;

        private ISceneFacade sceneFacade;

        private string path;

        public SceneFactoryShould(WebRequestsMode webRequestsMode)
        {
            this.webRequestsMode = webRequestsMode;
        }

        [Test]
        public async Task CreateSceneFacadeForTestScene()
        {
            sceneFacade = await sceneFactory.CreateSceneFromFileAsync(path, Substitute.For<IPartitionComponent>(), CancellationToken.None);

            var sceneFacadeImpl = (SceneFacade)sceneFacade;

            Assert.IsNotNull(sceneFacade);

            SceneInstanceDependencies.WithRuntimeAndJsAPIBase deps = sceneFacadeImpl.deps;

            Assert.IsNotNull(deps.Runtime);
            Assert.IsNotNull(deps.RuntimeImplementation);
            Assert.IsNotNull(deps.SyncDeps.CRDTWorldSynchronizer);
            Assert.IsNotNull(deps.SyncDeps.ExceptionsHandler);
            Assert.IsNotNull(deps.SyncDeps.SceneStateProvider);
            Assert.IsNotNull(deps.SyncDeps.PoolsProvider);
            Assert.IsNotNull(deps.SyncDeps.CRDTMemoryAllocator);

            Assert.AreNotEqual(default(World), sceneFacadeImpl.EcsExecutor.World);
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
