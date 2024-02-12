using Google.Protobuf;
using LiveKit.Internal.FFIClients.Pools;
using LiveKit.Internal.FFIClients.Pools.Memory;
using LiveKit.Proto;
using LiveKit.Rooms.DataPipes;
using System;
using System.Collections.Generic;

namespace DCL.Multiplayer.Connections.Messaging
{
    public struct MessageWrap<T> : IDisposable where T: class, IMessage, new()
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
            this.Payload = payload;
            this.dataPipe = dataPipe;
            this.recipients = recipients;
            this.multiPool = multiPool;
            this.memoryPool = memoryPool;
            sent = false;
        }

        public void Send(DataPacketKind dataPacketKind = DataPacketKind.KindLossy)
        {
            if (sent)
                throw new Exception("Request already sent");

            using var memory = memoryPool.Memory(Payload);
            var data = memory.Span();
            Payload.WriteTo(data);
            dataPipe.PublishData(data, TOPIC, recipients, dataPacketKind);
            sent = true;
        }

        public void AddRecipient(string sid)
        {
            recipients.Add(sid);
        }

        public void Dispose()
        {
            recipients.Clear();
            multiPool.Release(recipients);
            multiPool.Release(Payload);
        }
    }
}
