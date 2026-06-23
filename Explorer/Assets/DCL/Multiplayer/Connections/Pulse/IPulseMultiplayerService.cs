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

        /// <summary>
        ///     Connects and authenticates against the Pulse server.
        /// </summary>
        /// <param name="maxAttempts">
        ///     Upper bound on connection attempts before giving up on an unreachable server. The start-up
        ///     operation passes a small bound so it can fall back to LiveKit; runtime reconnection uses the
        ///     default to keep retrying.
        /// </param>
        /// <returns>
        ///     <c>true</c> once connected and authenticated; <c>false</c> when the server stays unreachable
        ///     after <paramref name="maxAttempts" /> attempts. Handshake/authentication failures throw instead.
        /// </returns>
        public UniTask<bool> ConnectAsync(CancellationToken ct, int maxAttempts = int.MaxValue);

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

            public UniTask<bool> ConnectAsync(CancellationToken ct, int maxAttempts = int.MaxValue) =>
                UniTask.FromResult(true);

            public UniTask DisconnectAsync() =>
                UniTask.CompletedTask;

            public void Send(OutgoingMessage outgoingMessage)
            {
                outgoingMessage.Dispose();
            }
        }
    }
}
