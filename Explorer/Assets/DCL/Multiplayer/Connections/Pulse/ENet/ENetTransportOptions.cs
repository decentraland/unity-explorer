namespace DCL.Multiplayer.Connections.Pulse.ENet
{
    public sealed class ENetTransportOptions
    {
        public ushort Port { get; set; } = 7777;
        public int ServiceTimeoutMs { get; set; } = 1;
    }
}
