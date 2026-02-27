namespace DCL.Multiplayer.Connections.Pulse.ENet
{
    public sealed class ENetTransportOptions
    {
        public const string SECTION_NAME = "Transport";

        public ushort Port { get; set; } = 7777;
        public int MaxPeers { get; set; } = 4095;
        public int ServiceTimeoutMs { get; set; } = 1;
    }
}
