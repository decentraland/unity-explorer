#nullable enable

using Arch.Core;
using Arch.SystemGroups;
using CommunicationData.URLHelpers;
using CRDT.Deserializer;
using CRDT.Memory;
using CRDT.Protocol;
using CRDT.Serializer;
using CrdtEcsBridge.Components;
using CrdtEcsBridge.ECSToCRDTWriter;
using CrdtEcsBridge.JsModulesImplementation.Communications;
using CrdtEcsBridge.OutgoingMessages;
using CrdtEcsBridge.PoolsProviders;
using CrdtEcsBridge.RestrictedActions;
using CrdtEcsBridge.UpdateGate;
using CrdtEcsBridge.WorldSynchronizer;
using Cysharp.Threading.Tasks;
using DCL.Interaction.Utility;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.PluginSystem.World.Dependencies;
using DCL.Profiles;
using DCL.Time;
using DCL.Utilities.Extensions;
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
using SceneRuntime.Apis.Modules;
using SceneRuntime.Apis.Modules.CommunicationsControllerApi;
using SceneRuntime.Apis.Modules.EngineApi;
using SceneRuntime.Apis.Modules.FetchApi;
using SceneRuntime.Apis.Modules.RestrictedActionsApi;
using SceneRuntime.Apis.Modules.Runtime;
using SceneRuntime.Apis.Modules.SceneApi;
using SceneRuntime.Factory;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Utility.Multithreading;

namespace SceneRunner.Tests
{
    [TestFixture]
    public class SceneFacadeShould
    {
        [SetUp]
        public void SetUp()
        {
            path = $"file://{Application.dataPath + "/../TestResources/Scenes/Cube/cube.js"}";

            sceneRuntimeFactory = new SceneRuntimeFactory(TestWebRequestController.INSTANCE, new IRealmData.Fake());

            ecsWorldFactory = Substitute.For<IECSWorldFactory>().EnsureNotNull();

            ecsWorldFactory.CreateWorld(in Arg.Any<ECSWorldFactoryArgs>())
                           .Returns(_ =>
                            {
                                var world = World.Create();
                                var builder = new ArchSystemsWorldBuilder<World>(world);

                                InitializationTestSystem1.InjectToWorld(ref builder);
                                SimulationTestSystem1.InjectToWorld(ref builder);
                                return new ECSWorldFacade(builder.Finish(), world, Array.Empty<IFinalizeWorldSystem>(), Array.Empty<ISceneIsCurrentListener>());
                            });

            sharedPoolsProvider = Substitute.For<ISharedPoolsProvider>().EnsureNotNull();
            crdtSerializer = Substitute.For<ICRDTSerializer>().EnsureNotNull();
            componentsRegistry = Substitute.For<ISDKComponentsRegistry>().EnsureNotNull();

            sceneFactory = new SceneFactory(ecsWorldFactory, sceneRuntimeFactory, sharedPoolsProvider, crdtSerializer, componentsRegistry,
                new SceneEntityFactory(), new EntityCollidersGlobalCache(), Substitute.For<IEthereumApi>(), Substitute.For<IMVCManager>(),
                Substitute.For<IProfileRepository>(), Substitute.For<IWeb3IdentityCache>(), IWebRequestController.DEFAULT,
                new IRoomHub.Fake(), Substitute.For<IRealmData>(), Substitute.For<ICommunicationControllerHub>());
        }

        [OneTimeTearDown]
        public async Task TearDown()
        {
            foreach (ISceneFacade sceneFacade in sceneFacades)
            {
                try { await sceneFacade.DisposeAsync(); }
                catch (Exception e) { Debug.LogException(e); }
            }

            sceneFacades.Clear();
        }

        private SceneRuntimeFactory sceneRuntimeFactory = null!;
        private IECSWorldFactory ecsWorldFactory = null!;
        private ISharedPoolsProvider sharedPoolsProvider = null!;
        private ICRDTDeserializer crdtDeserializer = null!;
        private ICRDTSerializer crdtSerializer = null!;
        private ISDKComponentsRegistry componentsRegistry = null!;
        private SceneFactory sceneFactory = null!;

        private readonly ConcurrentBag<ISceneFacade> sceneFacades = new ();

        private string path;

        [Test]
        public async Task ContinueUpdateLoopOnBackgroundThread([Values(5, 10, 20, 30, 60, 90, 180)] int fps, [Values(100, 500, 1000, 2000, 4000)] int lifeTimeMs)
        {
            ISceneFacade? sceneFacade = await sceneFactory.CreateSceneFromFileAsync(path, Substitute.For<IPartitionComponent>()!, CancellationToken.None);
            sceneFacades.Add(sceneFacade);

            var cancellationTokenSource = new CancellationTokenSource();

            cancellationTokenSource.CancelAfter(lifeTimeMs);

            // will end gracefully
            await sceneFacade.StartUpdateLoopAsync(fps, cancellationTokenSource.Token);

            // Asserts are inside the method
        }

        /*
        TODO: Temporarly commenting flaky test
        [Test]
        public async Task UpdateWithProperIntervals([Values(5, 10, 20, 40, 60, 90, 150)] int fps)
        {
            const int DURATION = 1000;

            ISceneRuntime sceneRuntime = Substitute.For<ISceneRuntime>()!;

            var sceneFacade = new SceneFacade(
                sceneRuntime,
                TestSystemsWorld.Create(),
                Substitute.For<ICRDTProtocol>()!,
                Substitute.For<IOutgoingCRDTMessagesProvider>()!,
                Substitute.For<ICRDTWorldSynchronizer>()!,
                Substitute.For<IInstancePoolsProvider>()!,
                Substitute.For<ICRDTMemoryAllocator>()!,
                Substitute.For<ISceneExceptionsHandler>()!,
                new SceneStateProvider(),
                Substitute.For<IEntityCollidersSceneCache>()!,
                Substitute.For<ISceneData>()!,
                new SceneEcsExecutor()
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
        */

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
        public async Task DisposeEverythingOnce()
        {
            const int DURATION = 1000;

            var sceneFacade = new SceneFacade(
                Substitute.For<ISceneData>(),
                new TestDeps(ecsWorldFactory)
            );

            var apis = new List<IJsApiWrapper>();

            var runtime = sceneFacade.deps.Runtime;
            runtime.When(r => r.Register(Arg.Any<string>(), Arg.Any<IJsApiWrapper>()))
                   .Do(info => apis.Add(info.ArgAt<IJsApiWrapper>(1)));

            runtime.When(r => r.Dispose())
                   .Do(_ => apis.ForEach(a => a.Dispose()));

            // Already mocked APIs
            runtime.Register(string.Empty, new TestAPIWrapper(sceneFacade.deps.SimpleFetchApi));
            runtime.Register(string.Empty, new TestAPIWrapper(sceneFacade.deps.WebSocketAipImplementation));
            runtime.Register(string.Empty, new TestAPIWrapper(sceneFacade.deps.CommunicationsControllerAPI));
            runtime.Register(string.Empty, new TestAPIWrapper(sceneFacade.deps.RuntimeImplementation));

            await UniTask.SwitchToThreadPool();

            // Provide basic Thread Pool synchronization context
            SynchronizationContext.SetSynchronizationContext(new SynchronizationContext());

            var cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.CancelAfter(DURATION);

            await sceneFacade.StartUpdateLoopAsync(10, cancellationTokenSource.Token);

            await UniTask.SwitchToMainThread();

            await sceneFacade.DisposeAsync();

            // Find all mocked disposable fields and make sure dispose was called on them
            foreach (FieldInfo field in sceneFacade.deps.GetType().GetFields())
            {
                if (!field.FieldType.IsValueType
                    && field.FieldType.GetInterface(nameof(IDisposable)) != null
                    && field.FieldType != typeof(SceneInstanceDependencies))
                {
                    var disposable = (IDisposable)field.GetValue(sceneFacade.deps);
                    disposable.Received(1).Dispose();
                }
            }

            // And in the base class
            foreach (FieldInfo field in sceneFacade.deps.SyncDeps.GetType().GetFields())
            {
                if (!field.FieldType.IsValueType && field.FieldType.GetInterface(nameof(IDisposable)) != null)
                {
                    var disposable = (IDisposable)field.GetValue(sceneFacade.deps.SyncDeps);
                    disposable.Received(1).Dispose();
                }
            }
        }

        [Test]
        public async Task DisposeInProperOrder()
        {
            const int DURATION = 1000;

            var sceneFacade = new SceneFacade(
                Substitute.For<ISceneData>(),
                new TestDeps(ecsWorldFactory)
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
                sceneFacade.deps.Runtime.Dispose();

                // World facade is not mockable
                // sceneFacade.deps.SyncDeps.ECSWorldFacade.Dispose();
                sceneFacade.deps.SyncDeps.CRDTProtocol.Dispose();
                sceneFacade.deps.SyncDeps.OutgoingCRDTMessagesProvider.Dispose();
                sceneFacade.deps.SyncDeps.CRDTWorldSynchronizer.Dispose();
                sceneFacade.deps.SyncDeps.PoolsProvider.Dispose();
                sceneFacade.deps.SyncDeps.CRDTMemoryAllocator.Dispose();

                sceneFacade.deps.SyncDeps.systemGroupThrottler.Dispose();
                sceneFacade.deps.SyncDeps.EntityCollidersCache.Dispose();
                sceneFacade.deps.SyncDeps.worldTimeProvider.Dispose();
                sceneFacade.deps.SyncDeps.ExceptionsHandler.Dispose();
            });
        }

        public class TestDeps : SceneInstanceDependencies.WithRuntimeAndJsAPIBase
        {
            public TestDeps(IECSWorldFactory worldFactory) : base(
                Substitute.For<IEngineApi>(),
                Substitute.For<IRestrictedActionsAPI>(),
                Substitute.For<IRuntime>(),
                Substitute.For<ISceneApi>(),
                Substitute.For<IWebSocketApi>(),
                Substitute.For<ISimpleFetchApi>(),
                Substitute.For<ICommunicationsControllerAPI>(),
                new SceneInstanceDependencies(
                    Substitute.For<ICRDTProtocol>(),
                    Substitute.For<IInstancePoolsProvider>(),
                    Substitute.For<ICRDTMemoryAllocator>(),
                    Substitute.For<IOutgoingCRDTMessagesProvider>(),
                    Substitute.For<IEntityCollidersSceneCache>(),
                    Substitute.For<ISceneStateProvider>(),
                    Substitute.For<ISceneExceptionsHandler>(),
                    worldFactory.CreateWorld(new ECSWorldFactoryArgs()),
                    Substitute.For<ICRDTWorldSynchronizer>(),
                    new URLAddress(),
                    new SceneEcsExecutor(),
                    Substitute.For<ISceneData>(),
                    new MutexSync(),
                    Substitute.For<ICRDTDeserializer>(),
                    Substitute.For<IECSToCRDTWriter>(),
                    Substitute.For<ISystemGroupsUpdateGate>(),
                    Substitute.For<IWorldTimeProvider>(),
                    new ECSWorldInstanceSharedDependencies()),
                Substitute.For<ISceneRuntime>()) { }
        }

        public class TestAPIWrapper : IJsApiWrapper
        {
            private readonly IDisposable api;

            public TestAPIWrapper(IDisposable api)
            {
                this.api = api;
            }

            public void Dispose()
            {
                api.Dispose();
            }
        }
    }
}
