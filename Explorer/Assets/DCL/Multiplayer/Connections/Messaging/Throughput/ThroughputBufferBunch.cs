namespace DCL.Multiplayer.Connections.Systems.Throughput
{
    public readonly struct ThroughputBufferBunch
    {
        public readonly ThroughputBuffer Incoming;
        public readonly ThroughputBuffer Outgoing;

        public ThroughputBufferBunch(ThroughputBuffer incoming, ThroughputBuffer outgoing)
        {
            this.Incoming = incoming;
            this.Outgoing = outgoing;
        }
    }
}
