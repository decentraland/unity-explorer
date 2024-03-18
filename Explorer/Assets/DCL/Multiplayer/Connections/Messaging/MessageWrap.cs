using Cysharp.Threading.Tasks;
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
            using MemoryWrap memory = memoryPool.Memory(Payload);
            Payload.WriteTo(memory);
            dataPipe.PublishData(memory.Span(), TOPIC, recipients, dataPacketKind);
            sent = true;
            Dispose();
        }

        public void AddRecipient(string sid)
        {
            recipients.Add(sid);
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
    }
}
