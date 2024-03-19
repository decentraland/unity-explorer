using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.Pools;
using Decentraland.Kernel.Comms.Rfc4;
using Google.Protobuf;
using LiveKit.Internal.FFIClients.Pools;
using LiveKit.Internal.FFIClients.Pools.Memory;
using LiveKit.Proto;
using LiveKit.Rooms;
using LiveKit.Rooms.DataPipes;
using System;
using System.Collections.Generic;

namespace DCL.Multiplayer.Connections.Messaging
{
    public struct MessageWrap<T> where T: class, IMessage, new()
    {
        private static readonly Type SELF_MESSAGE_TYPE = typeof(T);

        public readonly T Payload;
        private readonly IDataPipe dataPipe;
        private readonly List<string> recipients;
        private readonly IMultiPool multiPool;
        private readonly IMemoryPool memoryPool;
        private bool sent;

        private const string TOPIC = "";

        public MessageWrap(IDataPipe dataPipe, IMultiPool multiPool, IMemoryPool memoryPool) : this(
            multiPool.Get<T>(), dataPipe, multiPool.Get<List<string>>(), multiPool, memoryPool
        ) { }

        public MessageWrap(T payload, IDataPipe dataPipe, List<string> recipients, IMultiPool multiPool, IMemoryPool memoryPool)
        {
            Payload = payload;
            this.dataPipe = dataPipe;
            this.recipients = recipients;
            this.multiPool = multiPool;
            this.memoryPool = memoryPool;
            sent = false;
        }

        public async UniTaskVoid SendAndDisposeAsync(DataPacketKind dataPacketKind = DataPacketKind.KindLossy)
        {
            if (sent)
                throw new Exception("Request already sent");

            await UniTask.SwitchToThreadPool();
            using var packetWrap = multiPool.TempResource<Packet>();
            packetWrap.value.ClearMessage();
            WritePayloadToPacket(packetWrap.value);
            using MemoryWrap memory = memoryPool.Memory(packetWrap.value);
            packetWrap.value.WriteTo(memory);
            dataPipe.PublishData(memory.Span(), TOPIC, recipients, dataPacketKind);
            sent = true;
            Dispose();
        }

        public void AddRecipient(string sid)
        {
            recipients.Add(sid);
        }

        private void WritePayloadToPacket(Packet packet)
        {
            if (MessageWrapExtensions.WRITES_MAP.TryGetValue(SELF_MESSAGE_TYPE, out var writeAction) == false)
                throw new NotSupportedException($"Type {SELF_MESSAGE_TYPE.FullName} is not supported");

            writeAction!(packet, Payload);
        }

        private void Dispose()
        {
            recipients.Clear();
            multiPool.Release(recipients);
            multiPool.Release(Payload);
        }
    }

    public static class MessageWrapExtensions
    {
        public static void AddRecipients<T>(this MessageWrap<T> messageWrap, IReadOnlyCollection<string> sidList) where T: class, IMessage, new()
        {
            foreach (string s in sidList)
                messageWrap.AddRecipient(s);
        }

        public static void AddRecipients<T>(this MessageWrap<T> messageWrap, IRoom room) where T: class, IMessage, new()
        {
            messageWrap.AddRecipients(room.Participants.RemoteParticipantSids());
        }

        public static readonly IReadOnlyDictionary<Type, Action<Packet, object>> WRITES_MAP = new Dictionary<Type, Action<Packet, object>>
        {
            [typeof(AnnounceProfileVersion)] = (packet, o) => packet.ProfileVersion = (AnnounceProfileVersion)o,
            [typeof(Position)] = (packet, o) => packet.Position = (Position)o,
            [typeof(ProfileRequest)] = (packet, o) => packet.ProfileRequest = (ProfileRequest)o,
            [typeof(ProfileResponse)] = (packet, o) => packet.ProfileResponse = (ProfileResponse)o,
            [typeof(Scene)] = (packet, o) => packet.Scene = (Scene)o,
            [typeof(Voice)] = (packet, o) => packet.Voice = (Voice)o,
            [typeof(Decentraland.Kernel.Comms.Rfc4.Chat)] = (packet, o) => packet.Chat = (Decentraland.Kernel.Comms.Rfc4.Chat)o,
            [typeof(Decentraland.Kernel.Comms.Rfc4.Movement)] = (packet, o) => packet.Movement = (Decentraland.Kernel.Comms.Rfc4.Movement)o
        };
    }
}
