using Arch.Core;
using DCL.Multiplayer.Connections.Messaging;
using DCL.Multiplayer.Connections.Messaging.Hubs;
using DCL.Multiplayer.Connections.Messaging.Pipe;
using DCL.Multiplayer.Connections.Pools;
using DCL.Multiplayer.Connections.Rooms;
using DCL.Multiplayer.Profiles.Tables;
using Decentraland.Kernel.Comms.Rfc4;
using Google.Protobuf;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine.Pool;
using UnityEngine.TestTools;
using Utility.PriorityQueue;
using Debug = UnityEngine.Debug;
using MovementProto = Decentraland.Kernel.Comms.Rfc4.Movement;

namespace DCL.Multiplayer.Movement.Tests
{
    /// <summary>
    ///     Uncompressed movement subscriptions are registered with <see cref="IMessagePipe.ThreadStrict.OriginThread" />
    ///     rather than the default main-thread marshalling: the handler only runs the pure-static
    ///     <c>UncompressedMovementMessage</c> decode and writes <c>MovementInbox.Enqueue</c> (a thread-safe
    ///     <c>DCLConcurrentQueue</c> drained once per frame), so it is safe to run on the delivering thread and
    ///     skipping <c>UniTask.SwitchToMainThread()</c> for the highest-frequency comms message is safe.
    ///
    ///     These tests substitute the real <c>MessagePipe</c> with a recorder that reproduces its dispatch
    ///     contract (MainThreadOnly ⇒ marshal to the main-thread pump before invoking; OriginThread ⇒ invoke
    ///     inline on the delivering thread). The strict value the recorder branches on is exactly the one the
    ///     production ctor passes to Subscribe.
    /// </summary>
    [TestFixture]
    [Category("Performance")]
    public class LiveKitMovementReceiveThreadingShould
    {
        private const string WALLET = "0x0000000000000000000000000000000000000001";

        private RecordingHub hub;
        private IReadOnlyEntityParticipantTable participantTable;
        private MovementInbox inbox;
        private World world;
        private LiveKitMovementMessageBus bus;
        private int mainThreadId;

        [SetUp]
        public void SetUp()
        {
            LogAssert.ignoreFailingMessages = true;

            mainThreadId = Thread.CurrentThread.ManagedThreadId;

            hub = new RecordingHub();
            participantTable = Substitute.For<IReadOnlyEntityParticipantTable>();
            world = World.Create();
            inbox = new MovementInbox(participantTable, world);

            bus = new LiveKitMovementMessageBus(hub, inbox, null!);
        }

        [TearDown]
        public void TearDown()
        {
            bus.Dispose();
            world.Dispose();
            LogAssert.ignoreFailingMessages = false;
        }

        /// <summary>
        ///     The ctor must register the uncompressed Movement subscription with OriginThread (and the
        ///     compressed one with MainThreadOnly). Delivering K packets from a background thread must run the
        ///     decode+enqueue inline on that background thread — never marshalled to the main thread — and all K
        ///     must land in MovementInbox, observable after a single drain.
        /// </summary>
        [Test]
        public void DeliverUncompressedOnOriginThread_AllReachInboxAfterOneDrain()
        {
            const int K = 8;

            Assert.AreEqual(IMessagePipe.ThreadStrict.OriginThread, hub.Island.StrictOf(Packet.MessageOneofCase.Movement),
                "Uncompressed movement must subscribe with OriginThread to skip the per-packet SwitchToMainThread.");
            Assert.AreEqual(IMessagePipe.ThreadStrict.OriginThread, hub.Scene.StrictOf(Packet.MessageOneofCase.Movement),
                "Uncompressed movement (scene pipe) must subscribe with OriginThread too.");
            Assert.AreEqual(IMessagePipe.ThreadStrict.MainThreadOnly, hub.Island.StrictOf(Packet.MessageOneofCase.MovementCompressed),
                "Compressed movement must stay MainThreadOnly: a single wallet can be delivered concurrently by the "
                + "island and scene room threads, so its decoder's TimestampEncoder read-modify-write is unsafe off-thread.");

            (Entity _, RemotePlayerMovementComponent component) = RegisterEntity(WALLET);

            var pump = new List<Action>();
            var handlerThreadIds = new int[K];
            int workerThreadId = 0;

            Task worker = Task.Run(() =>
            {
                workerThreadId = Thread.CurrentThread.ManagedThreadId;

                for (var i = 0; i < K; i++)
                    handlerThreadIds[i] = Dispatch(hub.Island, Packet.MessageOneofCase.Movement, ProtoMessage(timestamp: i + 1), pump);
            });

            worker.Wait();

            Assert.AreEqual(0, pump.Count, "OriginThread must not marshal movement handlers onto the main-thread pump.");

            Assert.AreNotEqual(mainThreadId, workerThreadId, "Sanity: the worker must be a different thread than the test/main thread.");
            foreach (int id in handlerThreadIds)
            {
                Assert.AreEqual(workerThreadId, id, "Handler must execute on the delivering thread under OriginThread.");
                Assert.AreNotEqual(mainThreadId, id, "Handler must NOT execute on the main thread under OriginThread.");
            }

            inbox.DrainToEntities();
            Assert.AreEqual(K, component.Queue!.Count, "All K background-delivered movement messages must reach the inbox and drain to the entity.");
        }

        /// <summary>
        ///     N peers x ticks packets pumped from a worker thread. Under OriginThread the decode+enqueue
        ///     continuation runs entirely off the main thread — the main thread only pays a single per-frame
        ///     DrainToEntities. A MainThreadOnly pump of the same packet count forces all N*ticks handler
        ///     invocations onto the main-thread pump, for comparison. No GC assertion: UniTask pools its async
        ///     state machines, so this measures scheduling cost, not allocation.
        /// </summary>
        [Test]
        public void PumpFromWorker_MovesPerPacketHandlerCostOffMainThread()
        {
            const int PEERS = 50;
            const int TICKS = 10;
            const int TOTAL = PEERS * TICKS;

            var fixedPump = new List<Action>();
            int mainThreadHandlerRuns = 0;
            int workerHandlerRuns = 0;

            Task worker = Task.Run(() =>
            {
                int workerId = Thread.CurrentThread.ManagedThreadId;

                for (var p = 0; p < PEERS; p++)
                for (var t = 0; t < TICKS; t++)
                {
                    int ranOn = Dispatch(hub.Island, Packet.MessageOneofCase.Movement, ProtoMessage(timestamp: (p * TICKS) + t + 1), fixedPump);
                    if (ranOn == mainThreadId) Interlocked.Increment(ref mainThreadHandlerRuns);
                    else if (ranOn == workerId) Interlocked.Increment(ref workerHandlerRuns);
                }
            });
            worker.Wait();

            var mainDrain = Stopwatch.StartNew();
            inbox.DrainToEntities();
            mainDrain.Stop();

            Assert.AreEqual(0, mainThreadHandlerRuns, "Under OriginThread no movement handler may run on the main thread.");
            Assert.AreEqual(0, fixedPump.Count, "Under OriginThread nothing is marshalled to the main-thread pump.");
            Assert.AreEqual(TOTAL, workerHandlerRuns, "Every packet's decode+enqueue must run on the worker thread.");

            var baselinePump = new List<Action>();
            for (var i = 0; i < TOTAL; i++)
                DispatchAs(hub.Island, Packet.MessageOneofCase.Movement, ProtoMessage(timestamp: i + 1),
                    IMessagePipe.ThreadStrict.MainThreadOnly, baselinePump);

            Assert.AreEqual(TOTAL, baselinePump.Count,
                "MainThreadOnly defers all N*ticks handlers to the main thread.");

            var mainPump = Stopwatch.StartNew();
            foreach (Action a in baselinePump) a();
            mainPump.Stop();

            Debug.Log($"[perf] main-thread per-packet handlers — originThread: {mainThreadHandlerRuns}, " +
                      $"mainThreadOnly: {baselinePump.Count} | main-thread time originThread(drain only): " +
                      $"{mainDrain.Elapsed.TotalMilliseconds:F3} ms, mainThreadOnly(pump {TOTAL}): {mainPump.Elapsed.TotalMilliseconds:F3} ms");

            Assert.Less(mainThreadHandlerRuns, baselinePump.Count,
                "OriginThread delivery must move the per-packet continuation cost off the main thread.");
        }


        private (Entity entity, RemotePlayerMovementComponent component) RegisterEntity(string wallet)
        {
            var queuePool = new ObjectPool<SimplePriorityQueue<NetworkMovementMessage, double>>(
                () => new SimplePriorityQueue<NetworkMovementMessage, double>());

            var component = new RemotePlayerMovementComponent(queuePool);
            Entity entity = world.Create(component);

            participantTable.TryGet(wallet, out Arg.Any<IReadOnlyEntityParticipantTable.Entry>())
                            .Returns(ci =>
                             {
                                 ci[1] = new IReadOnlyEntityParticipantTable.Entry(wallet, entity, RoomSource.Island);
                                 return true;
                             });

            return (entity, component);
        }

        private static MovementProto ProtoMessage(int timestamp) =>
            new ()
            {
                Timestamp = timestamp,
                PositionX = 1f, PositionY = 2f, PositionZ = 3f,
                VelocityX = 0.5f, VelocityY = 0f, VelocityZ = 0.5f,
            };

        private int Dispatch(RecordingPipe pipe, Packet.MessageOneofCase ofCase, MovementProto proto, List<Action> mainThreadPump) =>
            DispatchAs(pipe, ofCase, proto, pipe.StrictOf(ofCase), mainThreadPump);

        private int DispatchAs(RecordingPipe pipe, Packet.MessageOneofCase ofCase, MovementProto proto,
            IMessagePipe.ThreadStrict strict, List<Action> mainThreadPump)
        {
            var handler = (Action<ReceivedMessage<MovementProto>>)pipe.HandlerOf(ofCase);
            ReceivedMessage<MovementProto> msg = Received(proto);

            if (strict == IMessagePipe.ThreadStrict.MainThreadOnly)
            {
                lock (mainThreadPump) mainThreadPump.Add(() => handler(msg));
                return -1;
            }

            int ranOn = Thread.CurrentThread.ManagedThreadId;
            handler(msg);
            return ranOn;
        }

        private ReceivedMessage<MovementProto> Received(MovementProto proto)
        {
            var packet = new Packet { Movement = proto };
            return new ReceivedMessage<MovementProto>(proto, packet, WALLET, new DCLMultiPool(), RoomSource.Island, string.Empty);
        }

        private sealed class RecordingHub : IMessagePipesHub
        {
            public readonly RecordingPipe Island = new ();
            public readonly RecordingPipe Scene = new ();
            public readonly RecordingPipe Chat = new ();

            public IMessagePipe ScenePipe() => Scene;
            public IMessagePipe IslandPipe() => Island;
            public IMessagePipe ChatPipe() => Chat;
            public void Dispose() { }
        }

        private sealed class RecordingPipe : IMessagePipe
        {
            private readonly Dictionary<Packet.MessageOneofCase, (object handler, IMessagePipe.ThreadStrict strict)> subs = new ();

            public IMessagePipe.ThreadStrict StrictOf(Packet.MessageOneofCase ofCase) => subs[ofCase].strict;
            public object HandlerOf(Packet.MessageOneofCase ofCase) => subs[ofCase].handler;

            public MessageWrap<T> NewMessage<T>(string topic) where T: class, IMessage, new() =>
                throw new NotSupportedException("Receive-path test does not send.");

            public void Subscribe<T>(Packet.MessageOneofCase ofCase, Action<ReceivedMessage<T>> onMessageReceived,
                IMessagePipe.ThreadStrict threadStrict) where T: class, IMessage, new() =>
                subs[ofCase] = (onMessageReceived, threadStrict);

            public void Dispose() { }
        }
    }
}
