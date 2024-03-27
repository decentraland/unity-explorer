using Arch.Core;
using Arch.SystemGroups;
using CRDT.Deserializer;
using CRDT.Memory;
using CRDT.Protocol;
using CRDT.Serializer;
using CrdtEcsBridge.Components;
using CrdtEcsBridge.OutgoingMessages;
using CrdtEcsBridge.PoolsProviders;
using CrdtEcsBridge.WorldSynchronizer;
using Cysharp.Threading.Tasks;
using DCL.Interaction.Utility;
using DCL.Profiles;
using DCL.Web3;
using DCL.Web3.Identities;
using DCL.WebRequests;
using ECS;
using ECS.LifeCycle;
using ECS.Prioritization.Components;
using ECS.TestSuite;
using MVC;
using NSubstitute;
using NUnit.Framework;
using SceneRunner.ECSWorld;
using SceneRunner.Scene;
using SceneRunner.Scene.ExceptionsHandling;
using SceneRunner.Tests.TestUtils;
using SceneRuntime;
using SceneRuntime.Factory;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace SceneRunner.Tests
{
    [TestFixture]
    public class SceneFacadeShould
    {
        [SetUp]
        public void SetUp()
        {
            path = $"file://{Application.dataPath + "/../TestResources/Scenes/Cube/cube.js"}";

            sceneRuntimeFactory = new SceneRuntimeFactory(TestWebRequestController.INSTANCE);

            ecsWorldFactory = Substitute.For<IECSWorldFactory>();

            ecsWorldFactory.CreateWorld(in Arg.Any<ECSWorldFactoryArgs>())
                           .Returns(_ =>
                            {
                                var world = World.Create();
                                var builder = new ArchSystemsWorldBuilder<World>(world);

                                InitializationTestSystem1.InjectToWorld(ref builder);
                                SimulationTestSystem1.InjectToWorld(ref builder);
                                return new ECSWorldFacade(builder.Finish(), world, Array.Empty<IFinalizeWorldSystem>());
                            });

            sharedPoolsProvider = Substitute.For<ISharedPoolsProvider>();
            crdtSerializer = Substitute.For<ICRDTSerializer>();
            componentsRegistry = Substitute.For<ISDKComponentsRegistry>();

            sceneFactory = new SceneFactory(ecsWorldFactory, sceneRuntimeFactory, sharedPoolsProvider, crdtSerializer, componentsRegistry,
                new SceneEntityFactory(), new EntityCollidersGlobalCache(), Substitute.For<IEthereumApi>(), Substitute.For<IMVCManager>(),
                Substitute.For<IProfileRepository>(), Substitute.For<IWeb3IdentityCache>(), IWebRequestController.DEFAULT, Substitute.For<IRealmData>());
        }

        [OneTimeTearDown]
        public async Task TearDown()
        {
            foreach (SceneFacade sceneFacade in sceneFacades)
            {
                try { await sceneFacade.DisposeAsync(); }
                catch (Exception e) { Debug.LogException(e); }
            }

            sceneFacades.Clear();
        }

        private SceneRuntimeFactory sceneRuntimeFactory;
        private IECSWorldFactory ecsWorldFactory;
        private ISharedPoolsProvider sharedPoolsProvider;
        private ICRDTDeserializer crdtDeserializer;
        private ICRDTSerializer crdtSerializer;
        private ISDKComponentsRegistry componentsRegistry;
        private SceneFactory sceneFactory;

        private readonly ConcurrentBag<SceneFacade> sceneFacades = new ();

        private string path;

        [Test]
        public async Task ContinueUpdateLoopOnBackgroundThread([Values(5, 10, 20, 30, 60, 90, 180)] int fps, [Values(100, 500, 1000, 2000, 4000)] int lifeTimeMs)
        {
            var sceneFacade = (SceneFacade)await sceneFactory.CreateSceneFromFileAsync(path, Substitute.For<IPartitionComponent>(), CancellationToken.None);
            sceneFacades.Add(sceneFacade);

            var cancellationTokenSource = new CancellationTokenSource();

            cancellationTokenSource.CancelAfter(lifeTimeMs);

            // will end gracefully
            await sceneFacade.StartUpdateLoopAsync(fps, cancellationTokenSource.Token);

            // Asserts are inside the method
        }

        [Test]
        public async Task UpdateWithProperIntervals([Values(5, 10, 20, 40, 60, 90, 150)] int fps)
        {
            const int DURATION = 1000;

            ISceneRuntime sceneRuntime = Substitute.For<ISceneRuntime>();

            var sceneFacade = new SceneFacade(
                sceneRuntime,
                TestSystemsWorld.Create(),
                Substitute.For<ICRDTProtocol>(),
                Substitute.For<IOutgoingCRDTMessagesProvider>(),
                Substitute.For<ICRDTWorldSynchronizer>(),
                Substitute.For<IInstancePoolsProvider>(),
                Substitute.For<ICRDTMemoryAllocator>(),
                Substitute.For<ISceneExceptionsHandler>(),
                new SceneStateProvider(),
                Substitute.For<IEntityCollidersSceneCache>(),
                Substitute.For<ISceneData>()
            );

            sceneFacades.Add(sceneFacade);

            await UniTask.SwitchToThreadPool();

            // Provide basic Thread Pool synchronization context
            SynchronizationContext.SetSynchronizationContext(new SynchronizationContext());

            float expectedDT = 1000f / fps;

            // -1 + StartScene(0) call
            var expectedCallsCount = (int)(DURATION / expectedDT);
            float expectedCallsCountTolerance = expectedCallsCount * 0.25f;
            expectedDT /= 1000f;

            var cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.CancelAfter(DURATION);

            await sceneFacade.StartUpdateLoopAsync(fps, cancellationTokenSource.Token);

            float tolerance = Mathf.Max(0.02f, expectedDT * 0.1f);

            await sceneRuntime.Received().UpdateScene(Arg.Is<float>(dt => Mathf.Approximately(dt, 0)));
            await sceneRuntime.Received().UpdateScene(Arg.Is<float>(dt => EqualWithTolerance(dt, expectedDT, tolerance)));
            await sceneRuntime.DidNotReceive().UpdateScene(Arg.Is<float>(dt => dt != 0 && !EqualWithTolerance(dt, expectedDT, tolerance)));

            int callsCount = sceneRuntime.ReceivedCalls().Count() - 1; // -1 stands for  StartScene

            Assert.AreEqual(expectedCallsCount, callsCount, expectedCallsCountTolerance);
        }

        private bool EqualWithTolerance(float dt, float expectedDT, float tolerance) =>
            dt >= expectedDT - tolerance && dt <= expectedDT + tolerance;

        [Test]
        [TestCase(new[] { 120, 60, 30 }, new[] { 200, 150, 500 })]
        [TestCase(new[] { 30, 20, 10, 5, 2 }, new[] { 300, 300, 300, 300, 300 })]
        [TestCase(new[] { 60, 60, 60, 60, 60 }, new[] { 300, 300, 300, 300, 300 })]
        public async Task UpdateMultipleInstancesAtDifferentRate(int[] fps, int[] lifeTimeMs)
        {
            int waitTime = lifeTimeMs.Max() + 100;

            var list = new ConcurrentBag<int>();

            async UniTask CreateAndLaunch(int fps, int lifeTime)
            {
                var sceneFacade = (SceneFacade)await sceneFactory.CreateSceneFromFileAsync(path, Substitute.For<IPartitionComponent>(), CancellationToken.None);
                sceneFacades.Add(sceneFacade);

                var cancellationTokenSource = new CancellationTokenSource();

                cancellationTokenSource.CancelAfter(lifeTime);

                // will end gracefully
                await sceneFacade.StartUpdateLoopAsync(fps, cancellationTokenSource.Token);

                list.Add(Thread.CurrentThread.ManagedThreadId);

                await Task.Delay(waitTime - lifeTime);
            }

            await UniTask.WhenAll(fps.Select((fps, i) => CreateAndLaunch(fps, lifeTimeMs[i])));

            // It is not reliable to count the threads exactly as the agent can have a limited capacity
            Assert.GreaterOrEqual(list.Distinct().Count(), Mathf.Min(2, fps.Length - 2));
        }

        [Test]
        public async Task DisposeInProperOrder()
        {
            const int DURATION = 1000;

            ISceneRuntime sceneRuntime = Substitute.For<ISceneRuntime>();

            var sceneFacade = new SceneFacade(
                sceneRuntime,
                TestSystemsWorld.Create(),
                Substitute.For<ICRDTProtocol>(),
                Substitute.For<IOutgoingCRDTMessagesProvider>(),
                Substitute.For<ICRDTWorldSynchronizer>(),
                Substitute.For<IInstancePoolsProvider>(),
                Substitute.For<ICRDTMemoryAllocator>(),
                Substitute.For<ISceneExceptionsHandler>(),
                new SceneStateProvider(),
                Substitute.For<IEntityCollidersSceneCache>(),
                Substitute.For<ISceneData>()
            );

            await UniTask.SwitchToThreadPool();

            // Provide basic Thread Pool synchronization context
            SynchronizationContext.SetSynchronizationContext(new SynchronizationContext());

            var cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.CancelAfter(DURATION);

            await sceneFacade.StartUpdateLoopAsync(10, cancellationTokenSource.Token);

            await UniTask.SwitchToMainThread();

            await sceneFacade.DisposeAsync();

            Received.InOrder(() =>
            {
                sceneRuntime.Dispose();

                // World facade is not mockable
                sceneFacade.crdtProtocol.Dispose();
                sceneFacade.outgoingCrtdMessagesProvider.Dispose();
                sceneFacade.crdtWorldSynchronizer.Dispose();
                sceneFacade.instancePoolsProvider.Dispose();
                sceneFacade.crdtMemoryAllocator.Dispose();
            });
        }
    }
}
