namespace DCL.Multiplayer.Connections.Pulse.ENet
{
    public sealed class ENetTransportOptions
    {
        public int ServiceTimeoutMs { get; set; } = 1;
        public int BufferSize { get; set; } = 4096;
    }
}
