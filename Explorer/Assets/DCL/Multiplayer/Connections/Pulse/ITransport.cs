using Cysharp.Threading.Tasks;
using Google.Protobuf;
using Pulse.Transport;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace DCL.Multiplayer.Connections.Pulse
{
    /// <summary>
    ///     Abstraction for the transport in case it's used actively from other services
    /// </summary>
    public interface ITransport : IDisposable
    {
        TransportState State { get; }

        long BytesSent { get; }
        long BytesReceived { get; }
        long PacketsSent { get; }
        long PacketsReceived { get; }

        UniTask ConnectAsync(string address, int port, CancellationToken ct);

        public Task DisconnectAsync(DisconnectReason reason);

        void Send(IMessage message, PacketMode mode);

        public enum TransportState
        {
            NONE,
            CONNECTING,
            CONNECTED,
            DISCONNECTING,
            DISCONNECTED,
        }
    }
}
