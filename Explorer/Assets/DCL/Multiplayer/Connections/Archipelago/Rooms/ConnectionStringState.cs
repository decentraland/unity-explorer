using REnum;

namespace DCL.Multiplayer.Connections.Archipelago.Rooms
{
    public readonly struct PendingConnection
    {
        public readonly string ConnectionString;

        public PendingConnection(string connectionString)
        {
            ConnectionString = connectionString;
        }
    }

    public readonly struct CurrentConnection
    {
        public readonly string ConnectionString;

        public CurrentConnection(string connectionString)
        {
            ConnectionString = connectionString;
        }
    }

    /// <summary>
    ///     State of the server-pushed connection string. <c>None</c> = nothing received yet;
    ///     <c>PendingConnection</c> = pushed by the server, not yet acted on by the cycle loop;
    ///     <c>CurrentConnection</c> = already consumed, only re-evaluated against room/backoff state.
    /// </summary>
    [REnum]
    [REnumFieldEmpty("None")]
    [REnumField(typeof(PendingConnection))]
    [REnumField(typeof(CurrentConnection))]
    public readonly partial struct ConnectionStringState
    {
        /// <summary>A pending string becomes current once read by the cycle loop; other states are unchanged.</summary>
        public ConnectionStringState Consume() =>
            IsPendingConnection(out PendingConnection pending)
                ? FromCurrentConnection(new CurrentConnection(pending.ConnectionString))
                : this;
    }
}
