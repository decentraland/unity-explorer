using Cysharp.Threading.Tasks;
using Google.Protobuf;
using System;
using System.Threading;

namespace DCL.Multiplayer.Connections.Pulse
{
    /// <summary>
    ///     Abstraction for the transport in case it's used actively from other services
    /// </summary>
    public interface ITransport : IDisposable
    {
        TransportState State { get; }

        UniTask ConnectAsync(string address, int port, CancellationToken ct);

        UniTask DisconnectAsync(DisconnectReason reason, CancellationToken ct);

        void Send(IMessage message, PacketMode mode);

        public enum PacketMode
        {
            RELIABLE = 0,
            UNRELIABLE_SEQUENCED = 1,
            UNRELIABLE_UNSEQUENCED = 2,
        }

        public enum TransportState
        {
            NONE,
            CONNECTING,
            CONNECTED,
            DISCONNECTING,
            DISCONNECTED,
        }

        public enum DisconnectReason
        {
            None = 0,
            Graceful = 1,           // clean shutdown / server stopping
            AuthTimeout = 2,        // PENDING_AUTH deadline exceeded
            AuthFailed = 3,         // handshake validation failed
            DuplicateSession = 4,   // evicted by newer connection with same player_id
            Kicked = 5,             // admin kick
            ServerFull = 6,
        }
    }
}
