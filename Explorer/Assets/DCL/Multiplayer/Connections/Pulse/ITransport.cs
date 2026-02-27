using Cysharp.Threading.Tasks;
using System;
using System.Threading;

namespace DCL.Multiplayer.Connections.Pulse
{
    /// <summary>
    ///     Abstraction for the transport in case it's used actively from other services
    /// </summary>
    public interface ITransport
    {
        UniTask ConnectAsync(Uri uri, CancellationToken ct);

        UniTask DisconnectAsync(CancellationToken ct);

        UniTask ListenForIncomingDataAsync(CancellationToken ct);

        public enum PacketMode
        {
            RELIABLE = 0,
            UNRELIABLE_SEQUENCED = 1,
            UNRELIABLE_UNSEQUENCED = 2,
        }
    }
}
