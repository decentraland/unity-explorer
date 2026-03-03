using Cysharp.Threading.Tasks;
using Google.Protobuf;
using System.Threading;

namespace DCL.Multiplayer.Connections.Pulse
{
    /// <summary>
    ///     Abstraction for the transport in case it's used actively from other services
    /// </summary>
    public interface ITransport
    {
        UniTask ConnectAsync(string address, int port, CancellationToken ct);

        UniTask DisconnectAsync(CancellationToken ct);

        UniTask ListenForIncomingDataAsync(CancellationToken ct);

        void Send(IMessage message, PacketMode mode);

        public enum PacketMode
        {
            RELIABLE = 0,
            UNRELIABLE_SEQUENCED = 1,
            UNRELIABLE_UNSEQUENCED = 2,
        }
    }
}
