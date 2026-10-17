using Decentraland.Pulse;
using Pulse.Transport;
using System;

namespace DCL.Multiplayer.Connections.Pulse
{
    public readonly struct OutgoingMessage : IDisposable
    {
        private static readonly ClientMessagePool POOL = new ();

        public readonly ClientMessage Message;
        public readonly PacketMode PacketMode;

        private OutgoingMessage(ClientMessage message, PacketMode packetMode)
        {
            Message = message;
            PacketMode = packetMode;
        }

        public void Dispose()
        {
            POOL.Release(Message);
        }

        public static OutgoingMessage Create(PacketMode packetMode, ClientMessage.MessageOneofCase kind) =>
            new (POOL.Get(kind), packetMode);
    }
}
