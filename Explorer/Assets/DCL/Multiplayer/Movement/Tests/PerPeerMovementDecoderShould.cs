using Arch.Core;
using DCL.Landscape.Settings;
using DCL.Multiplayer.Connections.Messaging;
using DCL.Multiplayer.Connections.Messaging.Hubs;
using DCL.Multiplayer.Connections.Messaging.Pipe;
using DCL.Multiplayer.Connections.Pools;
using DCL.Multiplayer.Connections.Rooms;
using DCL.Multiplayer.Movement.Settings;
using DCL.Multiplayer.Profiles.Tables;
using Decentraland.Kernel.Comms.Rfc4;
using Google.Protobuf;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.TestTools;
using Utility.PriorityQueue;
using CompressedProto = Decentraland.Kernel.Comms.Rfc4.MovementCompressed;

namespace DCL.Multiplayer.Movement.Tests
{
    /// <summary>
    ///     Each remote wallet decodes compressed movement through its own <see cref="NetworkMessageEncoder" />
    ///     (keyed by FromWalletId in the private <c>decodersByWallet</c>), so the sequentially-stateful
    ///     <see cref="TimestampEncoder" /> wraparound state (lastOriginalTimestamp / timestampOffset) is isolated
    ///     per peer instead of shared across every stream: one peer's timestamp code cannot trip another peer's
    ///     wraparound heuristic.
    ///
    ///     These tests drive the bus's real compressed receive handler (captured off the message pipe, exactly like
    ///     <see cref="LiveKitMovementReceiveThreadingShould" />) and read the decoded timestamps back out of the
    ///     per-peer inbox queue.
    /// </summary>
    [TestFixture]
    [Category("Performance")]
    public class PerPeerMovementDecoderShould
    {
        private const string WALLET_A = "0x00000000000000000000000000000000000000aa";
        private const string WALLET_B = "0x00000000000000000000000000000000000000bb";

        private static MessageEncodingSettings cachedSettings;

        private static MessageEncodingSettings Settings
        {
            get
            {
                if (cachedSettings == null)
                {
                    string[] guids = AssetDatabase.FindAssets("t:MessageEncodingSettings");
                    cachedSettings = AssetDatabase.LoadAssetAtPath<MessageEncodingSettings>(AssetDatabase.GUIDToAssetPath(guids[0]));
                }

                return cachedSettings;
            }
        }

        private RecordingHub hub;
        private IReadOnlyEntityParticipantTable participantTable;
        private MovementInbox inbox;
        private World world;
        private LiveKitMovementMessageBus bus;

        private int Steps => 1 << Settings.TIMESTAMP_BITS;
        private double BufferSize => Steps * (double)Settings.TIMESTAMP_QUANTUM;

        [SetUp]
        public void SetUp()
        {
            LogAssert.ignoreFailingMessages = true;

            hub = new RecordingHub();
            participantTable = Substitute.For<IReadOnlyEntityParticipantTable>();
            world = World.Create();
            inbox = new MovementInbox(participantTable, world);

            bus = new LiveKitMovementMessageBus(hub, inbox, null!);

            var parcelEncoder = new ParcelEncoder(ScriptableObject.CreateInstance<TerrainGenerationData>());
            bus.InitializeEncoder(Settings, Substitute.For<IMultiplayerMovementSettings>(), parcelEncoder);
        }

        [TearDown]
        public void TearDown()
        {
            bus.Dispose();
            world.Dispose();
            LogAssert.ignoreFailingMessages = false;
        }

        /// <summary>
        ///     Two interleaved peer streams must decode INDEPENDENTLY. Peer A sends a low (near buffer start)
        ///     code, peer B advances near the buffer end, then peer A repeats its low code. Because each peer owns
        ///     its own decoder, A never sees B's advance, so both of A's timestamps stay inside the first buffer —
        ///     a decoder shared across peers would flip A's second decode over the wraparound heuristic, adding a
        ///     phantom +BufferSize jump.
        /// </summary>
        [Test]
        public void InterleavedPeers_DecodeIndependently_NoCrossPeerWraparound()
        {
            (Entity _, RemotePlayerMovementComponent a) = RegisterEntity(WALLET_A);
            RegisterEntity(WALLET_B);

            int lowCode = Steps / 10;
            int highCode = (Steps * 9) / 10;

            DispatchCompressed(WALLET_A, lowCode);
            DispatchCompressed(WALLET_B, highCode);
            DispatchCompressed(WALLET_A, lowCode);

            inbox.DrainToEntities();

            List<double> aTimestamps = Drain(a);

            Assert.AreEqual(2, aTimestamps.Count, "Both of peer A's messages must reach A's inbox.");

            foreach (double ts in aTimestamps)
                Assert.Less(ts, BufferSize,
                    $"Peer A's decode must stay in the first buffer ({ts} < {BufferSize}). A value >= BufferSize means peer B's "
                    + "wraparound state leaked into A's decoder.");
        }

        /// <summary>
        ///     (2) A single peer's circular-buffer wraparound must still reconstruct correctly across the buffer
        ///     boundary. The bus's per-peer decoder for one wallet must produce exactly what a dedicated
        ///     <see cref="TimestampEncoder" /> produces for the same code stream — including the +BufferSize offset
        ///     applied when a code wraps from near the buffer end back to the start.
        /// </summary>
        [Test]
        public void SinglePeer_WraparoundReconstructsAcrossBufferBoundary()
        {
            (Entity _, RemotePlayerMovementComponent a) = RegisterEntity(WALLET_A);

            var codes = new[] { Steps / 2, (Steps * 3) / 4, Steps - 1, Steps / 8 };

            var reference = new TimestampEncoder(Settings);
            var expected = new double[codes.Length];
            for (var i = 0; i < codes.Length; i++)
                expected[i] = reference.Decompress(codes[i]);

            foreach (int code in codes)
                DispatchCompressed(WALLET_A, code);

            inbox.DrainToEntities();

            List<double> decoded = Drain(a);

            Assert.AreEqual(codes.Length, decoded.Count, "Every message from the single peer must be inboxed.");

            for (var i = 0; i < expected.Length; i++)
                Assert.AreEqual(expected[i], decoded[i], 1e-6,
                    $"Per-peer decode #{i} must match a dedicated TimestampEncoder, including the wraparound offset.");

            for (var i = 1; i < decoded.Count; i++)
                Assert.Greater(decoded[i], decoded[i - 1], "Reconstructed timestamps must stay strictly increasing across the wraparound.");
        }

        /// <summary>
        ///     (3) A departed peer must have its decoder evicted so <c>decodersByWallet</c> stays bounded to live
        ///     peers (the leak guard). First message lazily creates the entry; <see cref="LiveKitMovementMessageBus.EvictPeer" />
        ///     — the call <c>RemoteEntities.Remove</c> makes once a wallet has left every room — drops it.
        /// </summary>
        [Test]
        public void EvictPeer_DropsThePerPeerDecoder()
        {
            RegisterEntity(WALLET_A);

            IDictionary decoders = DecodersByWallet();
            Assert.AreEqual(0, decoders.Count, "No decoder should exist before the first message.");

            DispatchCompressed(WALLET_A, Steps / 4);
            inbox.DrainToEntities();

            Assert.AreEqual(1, decoders.Count, "The first message from a peer must lazily create exactly one decoder.");
            Assert.IsTrue(decoders.Contains(WALLET_A), "The decoder must be keyed by the peer's wallet id.");

            bus.EvictPeer(WALLET_A);

            Assert.AreEqual(0, decoders.Count, "EvictPeer must drop the departed peer's decoder (leak guard).");
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

        private void DispatchCompressed(string wallet, int timestampCode)
        {
            var handler = (Action<ReceivedMessage<CompressedProto>>)hub.Island.HandlerOf(Packet.MessageOneofCase.MovementCompressed);

            var proto = new CompressedProto { TemporalData = timestampCode, MovementData = 0, HeadSyncData = 0, PointAtData = 0 };
            var packet = new Packet { MovementCompressed = proto };
            var msg = new ReceivedMessage<CompressedProto>(proto, packet, wallet, new DCLMultiPool(), RoomSource.Island, string.Empty);

            handler(msg);
        }

        private static List<double> Drain(RemotePlayerMovementComponent component)
        {
            var outp = new List<double>();
            var queue = component.Queue!;
            while (queue.Count > 0)
                outp.Add(queue.Dequeue().timestamp);
            return outp;
        }

        private IDictionary DecodersByWallet()
        {
            FieldInfo field = typeof(LiveKitMovementMessageBus).GetField("decodersByWallet", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(field, "decodersByWallet field must exist on LiveKitMovementMessageBus.");
            return (IDictionary)field!.GetValue(bus);
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
