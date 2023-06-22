using Arch.Core;
using Arch.SystemGroups;
using CRDT.Deserializer;
using CRDT.Memory;
using CRDT.Protocol;
using CRDT.Serializer;
using CrdtEcsBridge.Components;
using CrdtEcsBridge.Engine;
using CrdtEcsBridge.OutgoingMessages;
using CrdtEcsBridge.UpdateGate;
using CrdtEcsBridge.WorldSynchronizer;
using Cysharp.Threading.Tasks;
using ECS.LifeCycle;
using NSubstitute;
using NUnit.Framework;
using SceneRunner.ECSWorld;
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
        private SceneRuntimeFactory sceneRuntimeFactory;
        private IECSWorldFactory ecsWorldFactory;
        private ISharedPoolsProvider sharedPoolsProvider;
        private ICRDTDeserializer crdtDeserializer;
        private ICRDTSerializer crdtSerializer;
        private ISDKComponentsRegistry componentsRegistry;
        private SceneFactory sceneFactory;

        private readonly ConcurrentBag<SceneFacade> sceneFacades = new ();

        private string path;

        [SetUp]
        public void SetUp()
        {
            path = $"file://{Application.dataPath + "/../TestResources/Scenes/Cube/cube.js"}";

            sceneRuntimeFactory = new SceneRuntimeFactory();

            ecsWorldFactory = Substitute.For<IECSWorldFactory>();

            ecsWorldFactory.CreateWorld(in Arg.Any<ECSWorldInstanceSharedDependencies>(), Arg.Any<ISystemGroupsUpdateGate>())
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

            sceneFactory = new SceneFactory(ecsWorldFactory, sceneRuntimeFactory, sharedPoolsProvider, crdtSerializer, componentsRegistry, new EntityFactory());
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

        [Test]
        public async Task ContinueUpdateLoopOnBackgroundThread([Values(5, 10, 20, 30, 60, 90, 180)] int fps, [Values(100, 500, 1000, 2000, 4000)] int lifeTimeMs)
        {
            var sceneFacade = (SceneFacade)await sceneFactory.CreateScene(path, CancellationToken.None);
            sceneFacades.Add(sceneFacade);

            var cancellationTokenSource = new CancellationTokenSource();

            cancellationTokenSource.CancelAfter(lifeTimeMs);

            // will end gracefully
            await sceneFacade.StartUpdateLoop(fps, cancellationTokenSource.Token);

            // Asserts are inside the method
        }

        [Test]
        public async Task UpdateWithProperIntervals([Values(5, 10, 20, 40, 60, 90, 220)] int fps)
        {
            const int DURATION = 1000;

            var sceneRuntime = Substitute.For<ISceneRuntime>();

            var sceneFacade = new SceneFacade(
                sceneRuntime,
                TestSystemsWorld.Create(),
                Substitute.For<ICRDTProtocol>(),
                Substitute.For<IOutgoingCRTDMessagesProvider>(),
                Substitute.For<ICRDTWorldSynchronizer>(),
                Substitute.For<IInstancePoolsProvider>(),
                Substitute.For<ICRDTMemoryAllocator>()
            );

            sceneFacades.Add(sceneFacade);

            await UniTask.SwitchToThreadPool();

            // Provide basic Thread Pool synchronization context
            SynchronizationContext.SetSynchronizationContext(new SynchronizationContext());

            var expectedDT = 1000f / fps;

            // -1 + StartScene(0) call
            var expectedCallsCount = (int)(DURATION / expectedDT);
            var expectedCallsCountTolerance = expectedCallsCount * 0.25f;
            expectedDT /= 1000f;

            var cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.CancelAfter(DURATION);

            await sceneFacade.StartUpdateLoop(fps, cancellationTokenSource.Token);

            var tolerance = Mathf.Max(0.02f, expectedDT * 0.1f);

            sceneRuntime.Received().UpdateScene(Arg.Is<float>(dt => Mathf.Approximately(dt, 0)));
            sceneRuntime.Received().UpdateScene(Arg.Is<float>(dt => EqualWithTolerance(dt, expectedDT, tolerance)));
            sceneRuntime.DidNotReceive().UpdateScene(Arg.Is<float>(dt => dt != 0 && !EqualWithTolerance(dt, expectedDT, tolerance)));

            var callsCount = sceneRuntime.ReceivedCalls().Count() - 1; // -1 stands for StartScene

            Assert.AreEqual(expectedCallsCount, callsCount, expectedCallsCountTolerance);
        }

        private bool EqualWithTolerance(float dt, float expectedDT, float tolerance) =>
            dt >= expectedDT - tolerance && dt <= expectedDT + tolerance;

        [Test]
        [TestCase(new int[] { 120, 60, 30 }, new int[] { 200, 150, 500 })]
        [TestCase(new int[] { 30, 20, 10, 5, 2 }, new int[] { 300, 300, 300, 300, 300 })]
        [TestCase(new int[] { 60, 60, 60, 60, 60 }, new int[] { 300, 300, 300, 300, 300 })]
        public async Task UpdateMultipleInstancesAtDifferentRate(int[] fps, int[] lifeTimeMs)
        {
            var waitTime = lifeTimeMs.Max() + 100;

            var list = new ConcurrentBag<int>();

            async UniTask CreateAndLaunch(int fps, int lifeTime)
            {
                var sceneFacade = (SceneFacade)await sceneFactory.CreateScene(path, CancellationToken.None);
                sceneFacades.Add(sceneFacade);

                var cancellationTokenSource = new CancellationTokenSource();

                cancellationTokenSource.CancelAfter(lifeTime);

                // will end gracefully
                await sceneFacade.StartUpdateLoop(fps, cancellationTokenSource.Token);

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
                Substitute.For<IOutgoingCRTDMessagesProvider>(),
                Substitute.For<ICRDTWorldSynchronizer>(),
                Substitute.For<IInstancePoolsProvider>(),
                Substitute.For<ICRDTMemoryAllocator>()
            );

            await UniTask.SwitchToThreadPool();

            // Provide basic Thread Pool synchronization context
            SynchronizationContext.SetSynchronizationContext(new SynchronizationContext());

            var cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.CancelAfter(DURATION);

            await sceneFacade.StartUpdateLoop(10, cancellationTokenSource.Token);

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
