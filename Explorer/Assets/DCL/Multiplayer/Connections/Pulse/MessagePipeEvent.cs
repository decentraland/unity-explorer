using Pulse.Transport;
using REnum;
using System;

namespace DCL.Multiplayer.Connections.Pulse
{
    /// <summary>
    ///     A tagged union representing either an incoming message or a transport disconnect event.
    ///     Both flow through the same channel so they are processed in wire order.
    /// </summary>
    [REnum]
    [REnumField(typeof(IncomingMessage), "Message")]
    [REnumField(typeof(DisconnectEvent))]
    public readonly partial struct MessagePipeEvent : IDisposable
    {
        public readonly struct DisconnectEvent
        {
            public readonly DisconnectReason Reason;

            public DisconnectEvent(DisconnectReason reason)
            {
                Reason = reason;
            }

            public static implicit operator DisconnectEvent(DisconnectReason reason) =>
                new (reason);

            public static implicit operator DisconnectReason(DisconnectEvent @event) =>
                @event.Reason;
        }

        public void Dispose()
        {
            Match(
                static m => m.Dispose(),
                static _ => { });
        }
    }
}
