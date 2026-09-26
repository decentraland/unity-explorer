using System;

namespace DCL.Multiplayer.Connections.Pulse
{
    /// <summary>
    ///     Wraps a transport-layer packet with enough metadata to parse and route it.
    ///     Call <see cref="Dispose" /> once raw bytes have been consumed to release native memory.
    /// </summary>
    public readonly ref struct MessagePacket
    {
        /// <summary>Pointer to native packet data. Valid only until <see cref="Dispose" /> is called.</summary>
        public readonly ReadOnlySpan<byte> Data;
        public readonly PeerId FromPeer;

        public MessagePacket(
            ReadOnlySpan<byte> data,
            PeerId fromPeer)
        {
            Data = data;
            FromPeer = fromPeer;
        }
    }
}
