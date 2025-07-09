using DCL.Multiplayer.Connections.Rooms;
using Decentraland.Kernel.Comms.Rfc4;
using Google.Protobuf;
using LiveKit.Internal.FFIClients.Pools;
using System;

namespace DCL.Multiplayer.Connections.Messaging
{
    public readonly struct ReceivedMessage<T> : IDisposable where T: class, IMessage, new()
    {
        public readonly T Payload;
        public readonly string FromWalletId;
        public readonly RoomSource FromRoom;
        public readonly string Topic;
        private readonly Packet packet;
        private readonly IMultiPool multiPool;

        public ReceivedMessage(T payload, Packet packet, string fromWalletId, IMultiPool multiPool, RoomSource fromRoom, string topic)
        {
            Payload = payload;
            FromWalletId = fromWalletId;
            this.multiPool = multiPool;
            FromRoom = fromRoom;
            this.packet = packet;
            Topic = topic;
        }

        public void Dispose()
        {
            packet.ClearMessage();
            multiPool.Release(packet);
            multiPool.Release(Payload);
        }
    }
}
