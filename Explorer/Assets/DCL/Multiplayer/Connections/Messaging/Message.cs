using Google.Protobuf;
using LiveKit.Internal.FFIClients.Pools;
using System;

namespace DCL.Multiplayer.Connections.Messaging
{
    public readonly struct Message<T> : IDisposable where T: class, IMessage, new()
    {
        public readonly T Payload;
        public readonly string FromSid;
        private readonly IMultiPool multiPool;

        public Message(T payload, string fromSid, IMultiPool multiPool)
        {
            this.Payload = payload;
            this.FromSid = fromSid;
            this.multiPool = multiPool;
        }

        public void Dispose()
        {
            multiPool.Release(Payload);
        }
    }
}
