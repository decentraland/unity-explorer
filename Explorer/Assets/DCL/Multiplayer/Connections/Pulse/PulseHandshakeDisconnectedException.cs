using Pulse.Transport;
using System;

namespace DCL.Multiplayer.Connections.Pulse
{
    /// <summary>
    ///     Signals a handshake that did not complete: the transport disconnected before a
    ///     <c>HandshakeResponse</c> arrived, or the server rejected the handshake.
    ///     <see cref="IsRetriable" /> tells the connection retry loop whether another attempt can
    ///     succeed (transient failures: shutdown, auth timeout, capacity pressure) or the failure
    ///     is terminal (rejection, ban, eviction, protocol violations).
    /// </summary>
    public class PulseHandshakeDisconnectedException : Exception
    {
        public bool IsRetriable { get; }

        public PulseHandshakeDisconnectedException(DisconnectReason reason)
            : base($"Pulse server disconnected during handshake: {reason}")
        {
            IsRetriable = IsRetriableReason(reason);
        }

        /// <summary>An explicit handshake rejection (failed <c>HandshakeResponse</c>) — always terminal.</summary>
        public PulseHandshakeDisconnectedException(string message) : base(message) { }

        /// <summary>
        ///     Transient reasons clear on their own timescale (server restart, pending-auth
        ///     pressure, capacity), so a fresh attempt with backoff can succeed. Everything else
        ///     is terminal: bans and evictions don't lift between attempts, and a rejected
        ///     payload would be resent identically. Unknown (future) reasons default to terminal
        ///     to avoid retry storms against new server-side protections.
        /// </summary>
        private static bool IsRetriableReason(DisconnectReason reason) =>
            reason switch
            {
                DisconnectReason.NONE
                    or DisconnectReason.GRACEFUL
                    or DisconnectReason.AUTH_TIMEOUT
                    or DisconnectReason.AUTH_FAILED
                    or DisconnectReason.SERVER_FULL
                    or DisconnectReason.PRE_AUTH_IP_LIMIT_EXHAUSTED
                    or DisconnectReason.PRE_AUTH_BUDGET_EXHAUSTED => true,
                _ => false,
            };
    }
}
