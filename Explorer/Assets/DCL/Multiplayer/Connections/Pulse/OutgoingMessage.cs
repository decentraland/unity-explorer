using Decentraland.Pulse;
using System;

namespace DCL.Multiplayer.Connections.Pulse
{
    public readonly struct OutgoingMessage : IDisposable
    {
        private static readonly ClientMessagePool POOL = new ();

        public readonly ClientMessage Message;
        public readonly ITransport.PacketMode PacketMode;

        private OutgoingMessage(ClientMessage message, ITransport.PacketMode packetMode)
        {
            Message = message;
            PacketMode = packetMode;
        }

        public void Dispose()
        {
            POOL.Release(Message);
        }

        public static OutgoingMessage Create(ITransport.PacketMode packetMode, ClientMessage.MessageOneofCase kind) =>
            new (POOL.Get(kind), packetMode);
    }
}
