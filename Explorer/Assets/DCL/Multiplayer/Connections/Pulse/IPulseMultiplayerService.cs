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
        public delegate (bool reconnectionAllowed, TimeSpan reconnectionDelay) DisconnectHandler(DisconnectReason disconnectReason);
        public delegate void IncomingMessageHandler(IncomingMessage message);
        public delegate UniTask HandshakeHandler(UniTaskCompletionSource<(bool success, string? error)> onHandshakeReceived, CancellationToken ct);

        public bool IsAuthenticated { get; }

        public void RegisterSyncHandler(ServerMessage.MessageOneofCase type, IncomingMessageHandler handler);

        public void RegisterDisconnectHandler(DisconnectHandler handler);

        public void RegisterHandshakeHandler(HandshakeHandler handler);

        public void UnregisterAllHandlers();

        public UniTask ConnectAsync(CancellationToken ct);

        public UniTask DisconnectAsync();

        public void Send(OutgoingMessage outgoingMessage);

        public class Dummy : IPulseMultiplayerService
        {
            public bool IsAuthenticated => false;

            public void Dispose() { }

            public void RegisterSyncHandler(ServerMessage.MessageOneofCase type, IncomingMessageHandler handler) { }

            public void RegisterDisconnectHandler(DisconnectHandler handler) { }

            public void RegisterHandshakeHandler(HandshakeHandler handler) { }

            public void UnregisterAllHandlers() { }

            public UniTask ConnectAsync(CancellationToken ct) =>
                UniTask.CompletedTask;

            public UniTask DisconnectAsync() =>
                UniTask.CompletedTask;

            public void Send(OutgoingMessage outgoingMessage)
            {
                outgoingMessage.Dispose();
            }
        }
    }
}
