namespace DCL.Multiplayer.Connections.Systems.Throughput
{
    public readonly struct ThroughputBufferBunch
    {
        public readonly IThroughputBuffer Incoming;
        public readonly IThroughputBuffer Outgoing;

        public ThroughputBufferBunch(IThroughputBuffer incoming, IThroughputBuffer outgoing)
        {
            this.Incoming = incoming;
            this.Outgoing = outgoing;
        }
    }
}
