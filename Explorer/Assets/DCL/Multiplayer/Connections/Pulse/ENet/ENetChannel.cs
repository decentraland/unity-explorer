namespace DCL.Multiplayer.Connections.Pulse.ENet
{
    /// <summary>
    ///     Reliable packets on a channel block sequenced unreliable packets on the same channel.
    ///     Here's what happens concretely:
    ///     Unreliable packets are still discarded if a newer sequence number has already been received — that's fine and expected.
    ///     But if a reliable packet is in-flight and unacknowledged, ENet will hold back subsequent sequenced unreliable packets on that same channel until the reliable one is ACK'd. This is head-of-line blocking — exactly what you're trying to avoid for position updates.
    ///     Unsequenced packets are immune to this — they bypass the sequence tracking entirely and will go through regardless of pending reliable packets on the channel.
    /// </summary>
    public readonly struct ENetChannel
    {
        public readonly byte ChannelId;
        public readonly PacketFlags PacketMode;

        public const int COUNT = 3;

        public static readonly ENetChannel RELIABLE = new (0, PacketFlags.Reliable);
        public static readonly ENetChannel UNRELIABLE_SEQUENCED = new (1, PacketFlags.None);
        public static readonly ENetChannel UNRELIABLE_UNSEQUENCED = new (2, PacketFlags.Unsequenced);

        public ENetChannel(byte channelId, PacketFlags packetMode)
        {
            ChannelId = channelId;
            PacketMode = packetMode;
        }
    }
}
