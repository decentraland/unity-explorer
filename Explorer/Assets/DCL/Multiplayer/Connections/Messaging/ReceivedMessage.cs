using Decentraland.Kernel.Comms.Rfc4;
using Google.Protobuf;
using LiveKit.Internal.FFIClients.Pools;
using System;

namespace DCL.Multiplayer.Connections.Messaging
{
    public readonly struct ReceivedMessage<T> : IDisposable where T: class, IMessage, new()
    {
        public readonly T Payload;
        public readonly string FromSid;
        private readonly Packet packet;
        private readonly IMultiPool multiPool;

        public ReceivedMessage(T payload, Packet packet, string fromSid, IMultiPool multiPool)
        {
            this.Payload = payload;
            this.FromSid = fromSid;
            this.multiPool = multiPool;
            this.packet = packet;
        }

        public void Dispose()
        {
            packet.ClearMessage();
            multiPool.Release(packet);
            multiPool.Release(Payload);
        }
    }
}
