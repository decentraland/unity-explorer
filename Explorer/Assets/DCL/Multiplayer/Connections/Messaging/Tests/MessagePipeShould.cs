using Cysharp.Threading.Tasks;
using DCL.LiveKit.Public;
using DCL.Multiplayer.Connections.Messaging.Pipe;
using DCL.Multiplayer.Connections.Rooms;
using DCL.Tests.Editor;
using Decentraland.Kernel.Comms.Rfc4;
using Google.Protobuf;
using LiveKit.Internal.FFIClients.Pools;
using LiveKit.Internal.FFIClients.Pools.Memory;
using LiveKit.Rooms.DataPipes;
using LiveKit.Rooms.Participants;
using NUnit.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine.TestTools;
using ChatPacket = Decentraland.Kernel.Comms.Rfc4.Chat;

namespace DCL.Multiplayer.Connections.Messaging.Tests
{
    public class MessagePipeShould
    {
        private const string SENDER_WALLET = "0x1111111111111111111111111111111111111111";
        private const string TOPIC = "test-topic";
        private const uint SUPPORTED_PROTOCOL_VERSION = 100;
        private const int MAX_FRAMES_TO_DELIVER = 60;

        private FakeDataPipe dataPipe = null!;
        private MessagePipe messagePipe = null!;

        [SetUp]
        public void SetUp()
        {
            dataPipe = new FakeDataPipe();
            messagePipe = new MessagePipe(dataPipe, new ThreadSafeMultiPool(), new ArrayMemoryPool(), RoomSource.Gatekeeper);
        }

        [TearDown]
        public void TearDown()
        {
            messagePipe.Dispose();
        }

        [UnityTest]
        public IEnumerator DeliverSenderIdentityWhenParticipantIsClearedBeforeMainThreadDispatch() =>
            UniTask.ToCoroutine(async () =>
            {
                // Arrange
                string? receivedFrom = null;

                messagePipe.Subscribe<ChatPacket>(Packet.MessageOneofCase.Chat, message =>
                {
                    receivedFrom = message.FromWalletId;
                    message.Dispose();
                }, IMessagePipe.ThreadStrict.MainThreadOnly);

                LKParticipant participant = LiveKitTestObjects.NewParticipant(SENDER_WALLET);
                byte[] data = new Packet { ProtocolVersion = SUPPORTED_PROTOCOL_VERSION, Chat = new ChatPacket { Message = "hi" } }.ToByteArray();

                // Act: LiveKit raises data on its FFI thread, then a room reconnection clears the pooled participant
                // before the main-thread continuation runs
                Task.Run(() => dataPipe.Raise(data, participant)).Wait();
                participant.Clear();

                for (var i = 0; i < MAX_FRAMES_TO_DELIVER && receivedFrom == null; i++)
                    await UniTask.Yield();

                // Assert
                Assert.That(receivedFrom, Is.EqualTo(SENDER_WALLET));
            });

        private class FakeDataPipe : IDataPipe
        {
            public event ReceivedDataDelegate? DataReceived;

            public void Raise(byte[] data, LKParticipant participant) =>
                DataReceived?.Invoke(data, participant, TOPIC, LKDataPacketKind.KindReliable);

            public void PublishData(Span<byte> data, string topic, IReadOnlyCollection<string> destinationSids, LKDataPacketKind kind = LKDataPacketKind.KindLossy) { }
        }
    }
}
