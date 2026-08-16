using Arch.Core;
using CRDT;
using CRDT.Deserializer;
using CRDT.Memory;
using CRDT.Protocol;
using CRDT.Protocol.Factory;
using CRDT.Serializer;
using CrdtEcsBridge.Components;
using CrdtEcsBridge.OutgoingMessages;
using CrdtEcsBridge.PoolsProviders;
using CrdtEcsBridge.UpdateGate;
using CrdtEcsBridge.WorldSynchronizer;
using DCL.Diagnostics;
using DCL.Profiling;
using NSubstitute;
using NUnit.Framework;
using SceneRunner.Scene.ExceptionsHandling;
using System;
using System.Collections.Generic;
using UnityEngine.TestTools;
using Utility.Multithreading;

namespace CrdtEcsBridge.JsModulesImplementation.Tests
{
    /// <summary>
    ///     The synchronizer holds a single rent slot: every buffer obtained by
    ///     <see cref="EngineAPIImplementation.CrdtSendToRenderer" /> must free it on every path,
    ///     including the failure ones, otherwise the scene is permanently wedged — each subsequent
    ///     rent waits the full timeout and throws "Rent Wait Timeout".
    /// </summary>
    public class EngineAPISyncBufferRentShould
    {
        private static readonly byte[] INPUT = { 0, 3, 5, 7, 10, 19, 20, 40, 76 };

        private World world;
        private MultiThreadSync multiThreadSync;
        private CRDTWorldSynchronizer crdtWorldSynchronizer;
        private ICRDTProtocol crdtProtocol;
        private ICRDTDeserializer crdtDeserializer;

        private EngineAPIImplementation engineAPIImplementation;

        [SetUp]
        public void SetUp()
        {
            world = World.Create();
            multiThreadSync = new MultiThreadSync(new SceneShortInfo());

            crdtWorldSynchronizer = new CRDTWorldSynchronizer(
                world,
                Substitute.For<ISDKComponentsRegistry>(),
                Substitute.For<ISceneEntityFactory>(),
                new Dictionary<CRDTEntity, Entity>());

            crdtProtocol = Substitute.For<ICRDTProtocol>();
            crdtDeserializer = Substitute.For<ICRDTDeserializer>();

            IInstancePoolsProvider instancePoolsProvider = Substitute.For<IInstancePoolsProvider>();
            instancePoolsProvider.GetDeserializationMessagesPool().Returns(_ => new List<CRDTMessage>());

            engineAPIImplementation = new EngineAPIImplementation(
                Substitute.For<ISharedPoolsProvider>(),
                instancePoolsProvider,
                crdtProtocol,
                crdtDeserializer,
                new NoOpCRDTSerializer(),
                crdtWorldSynchronizer,
                Substitute.For<IOutgoingCRDTMessagesProvider>(),
                Substitute.For<ISystemGroupsUpdateGate>(),

                // Swallows like the production pipeline (EngineApiWrapper reports and continues)
                Substitute.For<ISceneExceptionsHandler>(),
                multiThreadSync,
                new MultiThreadSync.Owner("TEST"),
                new SceneRuntimeMetrics());
        }

        [TearDown]
        public void TearDown()
        {
            // A run that failed with the slot leaked also leaks the pooled collections;
            // the resulting error log must not mask the assertion failure
            LogAssert.ignoreFailingMessages = true;

            crdtWorldSynchronizer.Dispose();
            multiThreadSync.Dispose();
            world.Dispose();

            LogAssert.ignoreFailingMessages = false;
        }

        [Test]
        public void ReleaseRentSlotWhenMutexAcquisitionFails()
        {
            // Deterministic stand-in for the production acquisition failures (10 s MultiThreadSync
            // timeout / disposal race): a disposed sync throws ObjectDisposedException at the same site
            multiThreadSync.Dispose();

            // The internal catch reports to the exceptions handler and returns normally
            engineAPIImplementation.CrdtSendToRenderer(INPUT, false);

            AssertRentSlotIsFree();
        }

        [Test]
        public void ReleaseRentSlotWhenMessageProcessingThrows()
        {
            crdtDeserializer.When(d => d.DeserializeBatch(ref Arg.Any<ReadOnlyMemory<byte>>(), Arg.Any<IList<CRDTMessage>>()))
                            .Do(c => c.ArgAt<IList<CRDTMessage>>(1)
                                      .Add(new CRDTMessage(CRDTMessageType.PUT_COMPONENT, 10, 100, 1, EmptyMemoryOwner<byte>.EMPTY)));

            crdtProtocol.ProcessMessage(Arg.Any<CRDTMessage>())
                        .Returns(_ => throw new InvalidOperationException("Corrupted CRDT message"));

            // Propagates to the caller (the production wrapper swallows it there)
            Assert.Throws<InvalidOperationException>(() => engineAPIImplementation.CrdtSendToRenderer(INPUT, false));

            AssertRentSlotIsFree();
        }

        private void AssertRentSlotIsFree()
        {
            IWorldSyncCommandBuffer recovered = null;

            // A leaked slot makes this rent wait the full RENT_WAIT_TIMEOUT (5 s)
            // and throw TimeoutException("Rent Wait Timeout: Couldn't rent command buffer")
            Assert.DoesNotThrow(
                () => recovered = crdtWorldSynchronizer.GetSyncCommandBuffer(),
                "The rent slot leaked: the failed CrdtSendToRenderer call did not release the sync command buffer");

            // Balance the probe rent so the synchronizer disposes cleanly
            recovered.FinalizeAndDeserialize();
            crdtWorldSynchronizer.ApplySyncCommandBuffer(recovered);
        }

        private class NoOpCRDTSerializer : ICRDTSerializer
        {
            public void Serialize(ref Span<byte> destination, in ProcessedCRDTMessage processedMessage) { }
        }
    }
}
