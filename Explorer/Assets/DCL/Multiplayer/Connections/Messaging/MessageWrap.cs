using CrdtEcsBridge.Components;
using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.Pools;
using Decentraland.Kernel.Comms.Rfc4;
using Google.Protobuf;
using LiveKit.Internal.FFIClients.Pools;
using LiveKit.Internal.FFIClients.Pools.Memory;
using LiveKit.Proto;
using LiveKit.Rooms.DataPipes;
using System;
using System.Collections.Generic;
using System.Threading;

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
        private readonly uint supportedVersion;
        private bool sent;
        private readonly string topic;

        public MessageWrap(IDataPipe dataPipe, IMultiPool multiPool, IMemoryPool memoryPool, string topic, uint supportedVersion) : this(
            multiPool.Get<T>(), dataPipe, multiPool.Get<List<string>>(), multiPool, memoryPool, topic, supportedVersion
        ) { }

        public MessageWrap(T payload, IDataPipe dataPipe, List<string> recipients, IMultiPool multiPool, IMemoryPool memoryPool, string topic,
            uint supportedVersion)
        {
            Payload = payload;
            this.dataPipe = dataPipe;
            this.recipients = recipients;
            this.multiPool = multiPool;
            this.memoryPool = memoryPool;
            this.supportedVersion = supportedVersion;
            this.topic = topic;
            sent = false;
        }

        public async UniTaskVoid SendAndDisposeAsync(CancellationToken cancellationToken, DataPacketKind dataPacketKind = DataPacketKind.KindLossy)
        {
            if (sent)
                throw new Exception("Request already sent");

            await UniTask.SwitchToThreadPool();

            if (cancellationToken.IsCancellationRequested)
                return;

            using var packetWrap = multiPool.TempResource<Packet>();
            packetWrap.value.ClearProtobufComponent();
            packetWrap.value.ProtocolVersion = supportedVersion;
            WritePayloadToPacket(packetWrap.value);
            using MemoryWrap memory = memoryPool.Memory(packetWrap.value);
            packetWrap.value.WriteTo(memory);
            packetWrap.value.ProtocolVersion = 0;
            dataPipe.PublishData(memory.Span(), topic, recipients, dataPacketKind);
            sent = true;
            Dispose();
        }

        /// <summary>
        /// Adding special participant removes broadcasting to all participants
        /// </summary>
        public void AddSpecialRecipient(string identity)
        {
            recipients.Add(identity);
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
        public static readonly IReadOnlyDictionary<Type, Action<Packet, object>> WRITES_MAP = new Dictionary<Type, Action<Packet, object>>
        {
            [typeof(AnnounceProfileVersion)] = (packet, o) => packet.ProfileVersion = (AnnounceProfileVersion)o,
            [typeof(Position)] = (packet, o) => packet.Position = (Position)o,
            [typeof(ProfileRequest)] = (packet, o) => packet.ProfileRequest = (ProfileRequest)o,
            [typeof(ProfileResponse)] = (packet, o) => packet.ProfileResponse = (ProfileResponse)o,
            [typeof(Scene)] = (packet, o) => packet.Scene = (Scene)o,
            [typeof(Voice)] = (packet, o) => packet.Voice = (Voice)o,
            [typeof(Chat)] = (packet, o) => packet.Chat = (Chat)o,
            [typeof(Movement)] = (packet, o) => packet.Movement = (Movement)o,
            [typeof(MovementCompressed)] = (packet, o) => packet.MovementCompressed = (MovementCompressed)o,
            [typeof(PlayerEmote)] = (packet, o) => packet.PlayerEmote = (PlayerEmote)o,
            [typeof(SceneEmote)] = (packet, o) => packet.SceneEmote = (SceneEmote)o
        };
    }
}
