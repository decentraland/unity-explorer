using Cysharp.Threading.Tasks;
using Decentraland.Pulse;
using Pulse.Transport;
using System;
using System.Threading;

namespace DCL.Multiplayer.Connections.Pulse
{
    /// <summary>
    ///     Exists because can be disabled by a feature flag
    /// </summary>
    public interface IPulseMultiplayerService : IDisposable
    {
        public bool IsAuthenticated { get; }

        public void Dispose();

        public void RegisterSyncHandler(ServerMessage.MessageOneofCase type, Action<IncomingMessage> handler);

        public void RegisterDisconnectHandler(Func<DisconnectReason, (bool reconnectionAllowed, TimeSpan reconnectionDelay)> handler);

        public void UnregisterAllHandlers();

        public UniTask ConnectAsync(CancellationToken ct);

        public void Disconnect();

        public void Send(OutgoingMessage outgoingMessage);

        public class Dummy : IPulseMultiplayerService
        {
            public bool IsAuthenticated => false;

            public void Dispose() { }

            public void RegisterSyncHandler(ServerMessage.MessageOneofCase type, Action<IncomingMessage> handler) { }

            public void RegisterDisconnectHandler(Func<DisconnectReason, (bool reconnectionAllowed, TimeSpan reconnectionDelay)> handler) { }

            public void UnregisterAllHandlers() { }

            public UniTask ConnectAsync(CancellationToken ct) =>
                UniTask.CompletedTask;

            public void Disconnect() { }

            public void Send(OutgoingMessage outgoingMessage)
            {
                outgoingMessage.Dispose();
            }
        }
    }
}
